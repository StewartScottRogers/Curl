using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Serves <c>ftp</c> URLs by handing the ones forwarded through an HTTP proxy to the HTTP
/// handler, as curl 8.21.0 forwards <c>curl -x http://p ftp://h/f</c> as
/// <c>GET ftp://h/f HTTP/1.1</c> (ADR-0056, rule 3; measured in BL-330's Notes).
/// </summary>
/// <remarks>
/// Only an <see cref="ProxyKind.Http" /> or <see cref="ProxyKind.Http10" /> proxy without
/// <c>-p</c> forwards. Any other <c>ftp</c> transfer - no proxy, <c>-p</c>, or a SOCKS or HTTPS
/// proxy - tunnels to an FTP server in curl, and there is no FTP handler yet, so it fails as an
/// unregistered scheme does: exit 1 with <c>Protocol "ftp" not supported</c>.
/// </remarks>
/// <param name="httpHandler">The HTTP handler a forwarded <c>ftp</c> transfer is performed by.</param>
internal sealed class ForwardedFtpProtocolHandler(IProtocolHandler httpHandler) : IProtocolHandler
{
    /// <summary>The one scheme this handler serves.</summary>
    private const string FtpScheme = "ftp";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = [FtpScheme];

    /// <summary>
    /// Performs a forwarded <c>ftp</c> transfer with the HTTP handler, and fails any other.
    /// </summary>
    /// <param name="context">The transfer to perform.</param>
    /// <returns>
    /// The HTTP handler's result for a forwarded transfer; otherwise exit 1
    /// (<see cref="CurlExitCode.UnsupportedProtocol" />) with <c>Protocol "ftp" not supported</c>.
    /// </returns>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
        IsForwarded(context)
            ? httpHandler.ExecuteAsync(context)
            : ValueTask.FromResult(TransferResult.Failure(
                CurlExitCode.UnsupportedProtocol,
                $"Protocol \"{FtpScheme}\" not supported"));

    /// <summary>Tells whether the transfer goes through an HTTP proxy without <c>-p</c>.</summary>
    private static bool IsForwarded(ITransferContext context) =>
        context.Http is { ForwardProxy.Kind: ProxyKind.Http or ProxyKind.Http10, ProxyTunnel: false };
}
