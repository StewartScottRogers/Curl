using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Serves the FTP handler's schemes, <c>ftp</c> and <c>ftps</c>, by routing each transfer to
/// the handler curl 8.21.0 performs it with: an <c>ftp</c> one forwarded through an HTTP proxy
/// goes to the HTTP handler, as curl forwards <c>curl -x http://p ftp://h/f</c> as
/// <c>GET ftp://h/f HTTP/1.1</c> (ADR-0056, rule 3; measured in BL-330's Notes), and every
/// other goes to the FTP handler (ADR-0323).
/// </summary>
/// <remarks>
/// Only an <c>ftp</c> transfer through an <see cref="ProxyKind.Http" /> or
/// <see cref="ProxyKind.Http10" /> proxy without <c>-p</c> forwards. Any other transfer - no
/// proxy, <c>-p</c>, a SOCKS or HTTPS proxy, or an <c>ftps</c> URL, which curl 8.21.0 tunnels
/// through an HTTP proxy with <c>CONNECT h:990</c> even without <c>-p</c> (measured, BL-458) -
/// talks FTP to the server, tunnelled through the proxy when there is one.
/// </remarks>
/// <param name="httpHandler">The HTTP handler a forwarded <c>ftp</c> transfer is performed by.</param>
/// <param name="ftpHandler">The FTP handler every other transfer is performed by, and whose schemes this handler serves.</param>
internal sealed class RoutingFtpProtocolHandler(IProtocolHandler httpHandler, IProtocolHandler ftpHandler) : IProtocolHandler
{
    /// <summary>The one scheme an HTTP proxy forwards.</summary>
    private const string FtpScheme = "ftp";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => ftpHandler.SupportedSchemes;

    /// <summary>
    /// Performs a forwarded <c>ftp</c> transfer with the HTTP handler, and any other with the
    /// FTP handler.
    /// </summary>
    /// <param name="context">The transfer to perform.</param>
    /// <returns>The result of the handler the transfer was routed to.</returns>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
        IsForwarded(context)
            ? httpHandler.ExecuteAsync(context)
            : ftpHandler.ExecuteAsync(context);

    /// <summary>Tells whether the transfer is an <c>ftp</c> one through an HTTP proxy without <c>-p</c>.</summary>
    private static bool IsForwarded(ITransferContext context) =>
        context.Url.Scheme == FtpScheme
        && context.Http is { ForwardProxy.Kind: ProxyKind.Http or ProxyKind.Http10, ProxyTunnel: false };
}
