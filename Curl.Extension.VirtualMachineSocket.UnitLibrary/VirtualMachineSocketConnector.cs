using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// Opens every connection of a transfer over one vsock or Hyper-V socket, whatever host and port its URL names, as
/// <c>--unix-socket</c> does over a Unix domain socket, and runs curl's TLS handshake over it for a TLS target, offering
/// curl's ALPN list for an <c>https://</c> one (ADR-1104).
/// </summary>
/// <param name="address">The socket's address.</param>
/// <param name="opener">Opens the socket.</param>
/// <param name="tlsProvider">
/// The TLS provider curl built from the option group's TLS options, layered over the socket for a TLS target; with
/// <see langword="null" />, a host that hands none, a TLS target is refused as not built in.
/// </param>
/// <param name="httpOverTlsApplicationProtocols">
/// What curl's own connector offers through ALPN for HTTP over TLS (<see cref="IConnector.HttpOverTlsApplicationProtocols" />),
/// offered for an <c>https</c> target that names no list of its own; <see langword="null" /> offers <c>http/1.1</c> alone.
/// </param>
internal sealed class VirtualMachineSocketConnector(
    VirtualMachineSocketAddress address,
    IVirtualMachineSocketOpener opener,
    ITlsProvider? tlsProvider = null,
    IReadOnlyList<string>? httpOverTlsApplicationProtocols = null) : IConnector
{
    private static readonly IReadOnlyList<string> Http11Only = ["http/1.1"];

    /// <inheritdoc />
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols => httpOverTlsApplicationProtocols ?? Http11Only;

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls && tlsProvider is null)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, $"TLS over the {address} is not built in");
        }

        Stream socket;
        try
        {
            socket = await opener.OpenAsync(address, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or UnauthorizedAccessException)
        {
            return ConnectResult.Failed(CurlExitCode.CouldntConnect, $"Failed to connect to the {address}: {exception.Message}");
        }

        VirtualMachineSocketConnection connection = new(socket);
        return target.UseTls
            ? await tlsProvider!.AuthenticateAsClientAsync(connection, target.TlsServerName ?? target.Host, target.Events, ApplicationProtocolsFor(target), cancellationToken).ConfigureAwait(false)
            : ConnectResult.Connected(connection);
    }

    /// <summary>
    /// Gives what the handshake for <paramref name="target" /> offers through ALPN, as curl's own connector chooses it:
    /// the target's own list or else <see cref="HttpOverTlsApplicationProtocols" /> for an <c>https</c> pool scheme, and
    /// nothing for any other TLS scheme.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <returns>The protocols to offer, in preference order.</returns>
    internal IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target) =>
        string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
            ? target.ApplicationProtocols ?? HttpOverTlsApplicationProtocols
            : [];
}
