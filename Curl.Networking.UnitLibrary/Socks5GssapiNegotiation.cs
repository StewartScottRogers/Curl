using System.Net.Security;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// SOCKS5 GSS-API authentication (RFC 1961) as curl 8.21.0 runs it once the proxy picks
/// method 1 (BL-615, ADR-0274): Kerberos context tokens in authentication messages (version 1,
/// type 1, a two-byte length) until the context is established, then the protection-level
/// message (type 2) offering no per-message protection - wrapped with the context, or bare
/// under <c>--socks5-gssapi-nec</c> - and the proxy's level read back. A proxy that grants
/// integrity or confidentiality fails, since curl protects no tunnel byte.
/// </summary>
internal static class Socks5GssapiNegotiation
{
    private const byte SubnegotiationVersion = 1;
    private const byte AuthenticationMessage = 1;
    private const byte ProtectionMessage = 2;
    private const byte RejectionMessage = 0xFF;
    private const byte NoProtection = 0;

    /// <summary>Runs the exchange.</summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="proxyHost">The proxy's host, which names the acceptor.</param>
    /// <param name="options">The service name, NEC mode, delegation, contexts and texts.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns><see langword="null" /> when authenticated without protection, else the exit 97 failure.</returns>
    public static async ValueTask<ConnectResult?> RunAsync(
        IConnection connection,
        string proxyHost,
        Socks5AuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        var texts = new Socks5GssapiFailureText(options.UsesSspiTexts, options.CredentialCacheName);
        if (options.SecurityContexts is not { } factory)
        {
            return SocksProxyTunnel.Failed(texts.ContextFailed(SecurityContextStatus.NoCredentials));
        }

        using var context = factory.Create(RequestFor(proxyHost, options));
        return await EstablishAsync(connection, context, texts, cancellationToken).ConfigureAwait(false)
            ?? await NegotiateProtectionAsync(connection, context, options.GssapiNec, texts, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The context request: Kerberos, the default credential, confidentiality asked as curl's
    /// SSPI build asks it, and the service joined to the proxy's host unless it holds a <c>/</c>,
    /// when it is the whole target.
    /// </summary>
    /// <param name="proxyHost">The proxy's host.</param>
    /// <param name="options">The service name and delegation.</param>
    /// <returns>The request.</returns>
    internal static SecurityContextRequest RequestFor(string proxyHost, Socks5AuthenticationOptions options)
    {
        var service = options.GssapiServiceName;
        var slash = service.IndexOf('/', StringComparison.Ordinal);
        var (serviceName, hostName) = slash < 0 ? (service, proxyHost) : (service[..slash], service[(slash + 1)..]);
        return new SecurityContextRequest(SecurityMechanism.Kerberos, serviceName, hostName)
        {
            Delegation = options.GssapiDelegation,
            MessageProtection = ProtectionLevel.EncryptAndSign,
        };
    }

    private static async ValueTask<ConnectResult?> EstablishAsync(
        IConnection connection,
        ISecurityContext context,
        Socks5GssapiFailureText texts,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> incoming = ReadOnlyMemory<byte>.Empty;
        while (true)
        {
            var step = await context.NextTokenAsync(incoming, cancellationToken).ConfigureAwait(false);
            if (step.Status is not (SecurityContextStatus.ContinueNeeded or SecurityContextStatus.Completed))
            {
                return SocksProxyTunnel.Failed(texts.ContextFailed(step.Status));
            }

            if (step.Token.Length > 0)
            {
                await SendMessageAsync(connection, AuthenticationMessage, step.Token, cancellationToken).ConfigureAwait(false);
            }

            if (step.Status == SecurityContextStatus.Completed)
            {
                return null;
            }

            var (token, failure) = await ReadMessageAsync(connection, AuthenticationMessage, texts.AuthenticationResponseLost, texts.InvalidAuthenticationResponse, texts.AuthenticationTokenLost, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            incoming = token;
        }
    }

    // One message from the proxy: a header of version, type and two-byte length, then the body.
    // A rejection or a type other than expectedType fails, as does a header or body cut short.
    private static async ValueTask<(byte[] Body, ConnectResult? Failure)> ReadMessageAsync(
        IConnection connection,
        byte expectedType,
        string headerLost,
        Func<byte[], string> invalidType,
        string bodyLost,
        CancellationToken cancellationToken)
    {
        var header = await SocksProxyTunnel.ReadReplyAsync(connection, 4, cancellationToken).ConfigureAwait(false);
        if (header is null)
        {
            return ([], SocksProxyTunnel.Failed(headerLost));
        }

        if (header[1] != expectedType)
        {
            return ([], SocksProxyTunnel.Failed(header[1] == RejectionMessage ? Socks5GssapiFailureText.Rejected(header) : invalidType(header)));
        }

        var body = await SocksProxyTunnel.ReadReplyAsync(connection, (header[2] << 8) | header[3], cancellationToken).ConfigureAwait(false);
        return body is null ? ([], SocksProxyTunnel.Failed(bodyLost)) : (body, null);
    }

    // curl works out the level the context could give, then offers none.
    private static async ValueTask<ConnectResult?> NegotiateProtectionAsync(
        IConnection connection,
        ISecurityContext context,
        bool nec,
        Socks5GssapiFailureText texts,
        CancellationToken cancellationToken)
    {
        byte[] level = [NoProtection];
        if ((nec ? level : context.Wrap(level, encrypt: false)) is not { } body)
        {
            return SocksProxyTunnel.Failed(texts.WrapFailed);
        }

        await SendMessageAsync(connection, ProtectionMessage, body, cancellationToken).ConfigureAwait(false);
        var (reply, failure) = await ReadMessageAsync(connection, ProtectionMessage, texts.EncryptionResponseLost, texts.InvalidEncryptionResponse, texts.EncryptionTypeLost, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        return CheckGrantedLevel(nec ? reply : context.Unwrap(reply), texts);
    }

    // The level must be one byte, and none: curl applies no protection to the tunnel.
    private static ConnectResult? CheckGrantedLevel(byte[]? granted, Socks5GssapiFailureText texts) => granted switch
    {
        null => SocksProxyTunnel.Failed(texts.UnwrapFailed),
        { Length: not 1 } => SocksProxyTunnel.Failed(texts.InvalidEncryptionLength(granted.Length)),
        [NoProtection] => null,
        _ => SocksProxyTunnel.Failed(Socks5GssapiFailureText.ProtectionNotImplemented),
    };

    private static ValueTask SendMessageAsync(IConnection connection, byte messageType, byte[] body, CancellationToken cancellationToken) =>
        SocksProxyTunnel.SendAsync(
            connection,
            [SubnegotiationVersion, messageType, (byte)(body.Length >> 8), (byte)body.Length, .. body],
            cancellationToken);
}
