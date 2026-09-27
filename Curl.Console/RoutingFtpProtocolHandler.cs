using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Serves <c>ftp</c> URLs by routing each transfer to the handler curl 8.21.0 performs it
/// with: one forwarded through an HTTP proxy goes to the HTTP handler, as curl forwards
/// <c>curl -x http://p ftp://h/f</c> as <c>GET ftp://h/f HTTP/1.1</c> (ADR-0056, rule 3;
/// measured in BL-330's Notes), and every other goes to the FTP handler (ADR-0093).
/// </summary>
/// <remarks>
/// Only an <see cref="ProxyKind.Http" /> or <see cref="ProxyKind.Http10" /> proxy without
/// <c>-p</c> forwards. Any other <c>ftp</c> transfer - no proxy, <c>-p</c>, or a SOCKS or HTTPS
/// proxy - talks FTP to the server, tunnelled through the proxy when there is one.
/// </remarks>
/// <param name="httpHandler">The HTTP handler a forwarded <c>ftp</c> transfer is performed by.</param>
/// <param name="ftpHandler">The FTP handler every other <c>ftp</c> transfer is performed by.</param>
internal sealed class RoutingFtpProtocolHandler(IProtocolHandler httpHandler, IProtocolHandler ftpHandler) : IProtocolHandler
{
    /// <summary>The one scheme this handler serves.</summary>
    private const string FtpScheme = "ftp";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = [FtpScheme];

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

    /// <summary>Tells whether the transfer goes through an HTTP proxy without <c>-p</c>.</summary>
    private static bool IsForwarded(ITransferContext context) =>
        context.Http is { ForwardProxy.Kind: ProxyKind.Http or ProxyKind.Http10, ProxyTunnel: false };
}
