using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The SOCKS5 and SOCKS5h handshake (RFC 1928, with RFC 1929 user name and password) as
/// curl 8.21.0 runs it (measured): a greeting offering no authentication, GSSAPI unless
/// <c>--socks5-basic</c> alone was given, and user name and password when there is a credential
/// unless <c>--socks5-gssapi</c> alone was given; RFC 1929's sub-negotiation or RFC 1961's GSS-API
/// exchange (<see cref="Socks5GssapiNegotiation" />) when the proxy picks it; then one CONNECT
/// request and its reply.
/// </summary>
/// <remarks>
/// SOCKS5 resolves the host locally and sends its first address; SOCKS5h sends the host
/// name for the proxy to resolve. Both send an address literal as an address. A proxy that
/// picks a method that was not allowed fails with curl's message for it. ADR-0084 and
/// ADR-0276 record these choices.
/// </remarks>
internal static class Socks5Handshake
{
    /// <summary>The most bytes a SOCKS5h host name may hold (measured: 256 is refused).</summary>
    public const int MaximumHostBytes = 255;

    /// <summary>The most bytes the user name or the password may hold (measured: 256 is refused).</summary>
    public const int MaximumCredentialBytes = 255;

    private const byte NoAuthentication = 0;
    private const byte Gssapi = 1;
    private const byte UserNameAndPassword = 2;
    private const byte NoAcceptableMethod = 0xFF;

    /// <summary>
    /// Runs the handshake.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="proxy">The SOCKS5 or SOCKS5h proxy.</param>
    /// <param name="host">The host the tunnel reaches.</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="resolve">Resolves the host for SOCKS5.</param>
    /// <param name="authentication">The methods allowed, and how GSS-API runs.</param>
    /// <param name="events">Receives the <c>-v</c> lines a failed GSS-API negotiation prints.</param>
    /// <param name="trace">Receives the <c>[SOCKS]</c> lines, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns><see langword="null" /> when the tunnel is open, else the failure.</returns>
    public static async ValueTask<ConnectResult?> RunAsync(
        IConnection connection,
        ProxyEndpoint proxy,
        string host,
        int port,
        Func<string, int, CancellationToken, ValueTask<DnsResolution>> resolve,
        Socks5AuthenticationOptions authentication,
        ITransferEvents events,
        ITransferEvents? trace,
        CancellationToken cancellationToken)
    {
        // curl 8.21.0 names the destination as given before anything else (BL-1191 Notes).
        SocksProxyTunnel.Trace(trace, string.Create(CultureInfo.InvariantCulture, $"SOCKS5: connecting to {host}:{port}"));
        var literal = SocksProxyTunnel.ParseAddressLiteral(host);
        var hostName = proxy.Kind == ProxyKind.Socks5Hostname && literal is null ? Encoding.UTF8.GetBytes(host) : null;
        if (hostName?.Length > MaximumHostBytes)
        {
            return SocksProxyTunnel.Failed("SOCKS5: the destination hostname is too long to be resolved remotely by the proxy.");
        }

        var destination = new Socks5Destination(host, port, hostName, literal, proxy.Kind == ProxyKind.Socks5Hostname);
        return await NegotiateAuthenticationAsync(connection, proxy, authentication, events, trace, cancellationToken).ConfigureAwait(false)
            ?? await RequestConnectAsync(connection, destination, resolve, trace, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ConnectResult?> NegotiateAuthenticationAsync(
        IConnection connection,
        ProxyEndpoint proxy,
        Socks5AuthenticationOptions authentication,
        ITransferEvents events,
        ITransferEvents? trace,
        CancellationToken cancellationToken)
    {
        // Without user name and password allowed, curl forgets the credential (measured).
        var credential = authentication.AllowUserNameAndPassword ? proxy.Credential : null;
        await SocksProxyTunnel.SendAsync(connection, Greeting(authentication.AllowGssapi, credential is not null), cancellationToken).ConfigureAwait(false);
        SocksProxyTunnel.Trace(trace, "adjust pollset in (7)");

        return await SocksProxyTunnel.ReadReplyAsync(connection, 2, cancellationToken).ConfigureAwait(false) switch
        {
            null => SocksProxyTunnel.Failed(SocksProxyTunnel.ProxyClosedMessage),
            [not 5, _] => SocksProxyTunnel.Failed("Received invalid version in initial SOCKS5 response."),
            [_, NoAuthentication] => null,
            [_, UserNameAndPassword] when !authentication.AllowUserNameAndPassword =>
                SocksProxyTunnel.Failed("BASIC authentication proposed but not enabled."),
            [_, UserNameAndPassword] => await AuthenticateAsync(connection, credential, cancellationToken).ConfigureAwait(false),
            [_, Gssapi] when !authentication.AllowGssapi =>
                SocksProxyTunnel.Failed("SOCKS5 GSSAPI per-message authentication is not enabled."),
            [_, Gssapi] => await Socks5GssapiNegotiation.RunAsync(connection, proxy.Host, authentication, events, cancellationToken).ConfigureAwait(false),
            [_, NoAcceptableMethod] => SocksProxyTunnel.Failed("No authentication method was acceptable."),
            _ => SocksProxyTunnel.Failed("Unknown SOCKS5 mode attempted to be used by server."),
        };
    }

    // Version 5, the method count, then no authentication, GSSAPI and user name and password
    // as allowed, in that order (measured).
    private static byte[] Greeting(bool offerGssapi, bool offerUserNameAndPassword)
    {
        List<byte> methods = [NoAuthentication];
        if (offerGssapi)
        {
            methods.Add(Gssapi);
        }

        if (offerUserNameAndPassword)
        {
            methods.Add(UserNameAndPassword);
        }

        return [5, (byte)methods.Count, .. methods];
    }

    // RFC 1929.
    private static async ValueTask<ConnectResult?> AuthenticateAsync(
        IConnection connection,
        NetworkCredential? credential,
        CancellationToken cancellationToken)
    {
        var (request, failure) = BuildAuthenticationRequest(credential);
        if (failure is not null)
        {
            return failure;
        }

        await SocksProxyTunnel.SendAsync(connection, request, cancellationToken).ConfigureAwait(false);
        return await SocksProxyTunnel.ReadReplyAsync(connection, 2, cancellationToken).ConfigureAwait(false) switch
        {
            null => SocksProxyTunnel.Failed(SocksProxyTunnel.ProxyClosedMessage),
            [_, 0] => null,
            { } status => SocksProxyTunnel.Failed($"User was rejected by the SOCKS5 server ({status[0]} {status[1]})."),
        };
    }

    // With no credential curl still answers, with an empty user name and password (measured).
    private static (byte[] Request, ConnectResult? Failure) BuildAuthenticationRequest(NetworkCredential? credential)
    {
        var userName = EncodeUtf8(credential?.UserName);
        var password = EncodeUtf8(credential?.Password);
        if (userName.Length > MaximumCredentialBytes)
        {
            return ([], SocksProxyTunnel.Failed("Excessive username length for proxy auth"));
        }

        return password.Length > MaximumCredentialBytes
            ? ([], SocksProxyTunnel.Failed("Excessive password length for proxy auth"))
            : (FormatAuthenticationRequest(userName, password), null);
    }

    private static byte[] EncodeUtf8(string? text) => Encoding.UTF8.GetBytes(text ?? string.Empty);

    private static byte[] FormatAuthenticationRequest(byte[] userName, byte[] password) =>
        [1, (byte)userName.Length, .. userName, (byte)password.Length, .. password];

    private static async ValueTask<ConnectResult?> RequestConnectAsync(
        IConnection connection,
        Socks5Destination destination,
        Func<string, int, CancellationToken, ValueTask<DnsResolution>> resolve,
        ITransferEvents? trace,
        CancellationToken cancellationToken)
    {
        var (host, port) = (destination.Host, destination.Port);
        var (address, failure) = destination.HostName is { } hostName
            ? ([3, (byte)hostName.Length, .. hostName], null)
            : await ResolveAddressAsync(host, port, destination.Literal, resolve, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        // SOCKS5h names the host as given, an address literal too; SOCKS5 the address it sends,
        // an IPv6 one in brackets (measured, BL-1191 Notes).
        SocksProxyTunnel.Trace(trace, destination.ResolvedRemotely
            ? string.Create(CultureInfo.InvariantCulture, $"SOCKS5 connect to {host}:{port} (remotely resolved)")
            : string.Create(CultureInfo.InvariantCulture, $"SOCKS5 connect to {PrintableAddress(address)}:{port} (locally resolved)"));
        byte[] request = [5, 1, 0, .. address, (byte)(port >> 8), (byte)port];
        await SocksProxyTunnel.SendAsync(connection, request, cancellationToken).ConfigureAwait(false);
        SocksProxyTunnel.Trace(trace, "adjust pollset in (15)");
        var result = await ReadConnectReplyAsync(connection, host, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            SocksProxyTunnel.Trace(trace, "SOCKS5 request granted.");
        }

        return result;
    }

    // The address field of a request whose address type is IPv4 (1) or IPv6 (4), as curl prints it.
    private static string PrintableAddress(byte[] address)
    {
        var printed = new IPAddress(address.AsSpan(1)).ToString();
        return address[0] == 4 ? $"[{printed}]" : printed;
    }

    private static async ValueTask<(byte[] Address, ConnectResult? Failure)> ResolveAddressAsync(
        string host,
        int port,
        IPAddress? literal,
        Func<string, int, CancellationToken, ValueTask<DnsResolution>> resolve,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = literal is not null
            ? new DnsResolution([literal], DnsLookupFailure.None)
            : await resolve(host, port, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return ([], SocksProxyTunnel.CouldNotResolve(host, port, failure));
        }

        var address = addresses[0];
        var addressType = address.AddressFamily == AddressFamily.InterNetworkV6 ? (byte)4 : (byte)1;
        return ([addressType, .. address.GetAddressBytes()], null);
    }

    // The host and port the tunnel reaches; HostName is the name SOCKS5h sends, or null to send
    // the host's address; ResolvedRemotely is whether the proxy is SOCKS5h.
    private readonly record struct Socks5Destination(string Host, int Port, byte[]? HostName, IPAddress? Literal, bool ResolvedRemotely);

    // The reply is the version, the status, a reserved byte and the bound address, whose
    // length its type gives; all of it is read so the tunnel starts at the next byte.
    private static async ValueTask<ConnectResult?> ReadConnectReplyAsync(IConnection connection, string host, CancellationToken cancellationToken)
    {
        if (await SocksProxyTunnel.ReadReplyAsync(connection, 5, cancellationToken).ConfigureAwait(false) is not { } header)
        {
            return SocksProxyTunnel.Failed(SocksProxyTunnel.ProxyClosedMessage);
        }

        var (remainingBytes, failure) = CheckConnectReplyHeader(header, host);
        if (failure is not null)
        {
            return failure;
        }

        return await SocksProxyTunnel.ReadReplyAsync(connection, remainingBytes, cancellationToken).ConfigureAwait(false) is null
            ? SocksProxyTunnel.Failed(SocksProxyTunnel.ProxyClosedMessage)
            : null;
    }

    // Checks the reply's first five bytes, and gives the number of bytes still to read.
    private static (int RemainingBytes, ConnectResult? Failure) CheckConnectReplyHeader(byte[] header, string host)
    {
        if (header[0] != 5)
        {
            return (0, SocksProxyTunnel.Failed("SOCKS5 reply has wrong version, version should be 5."));
        }

        if (header[1] != 0)
        {
            return (0, SocksProxyTunnel.Failed($"cannot complete SOCKS5 connection to {host}. ({header[1]})"));
        }

        return RemainingAfterHeader(header[3], header[4]) is { } remainingBytes
            ? (remainingBytes, null)
            : (0, SocksProxyTunnel.Failed("SOCKS5 reply has wrong address type."));
    }

    // The bytes after the fifth: the rest of an IPv4 or IPv6 address, or a host name whose
    // length is the fifth byte; then the two-byte port. Null for an unknown address type.
    private static int? RemainingAfterHeader(byte addressType, byte fifthByte) => addressType switch
    {
        1 => 3 + 2,
        4 => 15 + 2,
        3 => fifthByte + 2,
        _ => null,
    };
}
