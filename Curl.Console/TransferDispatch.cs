using Curl.Core;

namespace Curl.Console;

/// <summary>
/// What <see cref="CurlCommandRunner" /> transfers through for one run: the dispatcher, the
/// warning lines curl prints before each transfer for options the build ignores, and the run's
/// cookies.
/// </summary>
/// <param name="dispatcher">Performs each transfer with the handler for its scheme.</param>
/// <param name="warningLinesBeforeEachTransfer">
/// The lines written to standard error before every URL's transfer unless <c>-s</c> is given,
/// such as the Schannel build's <c>--capath</c> warning (ADR-0009); each without its line
/// ending, and a <c>Warning: </c> line unwrapped, since the runner wraps it at the terminal
/// width. Empty when no option raises one.
/// </param>
/// <param name="cookies">
/// The run's cookies, which the dispatcher's HTTP handler reads and writes, or
/// <see langword="null" /> when neither <c>-b</c> nor <c>-c</c> was given.
/// </param>
/// <param name="proxySelector">
/// Chooses each transfer's proxy from <c>-x</c>, <c>--noproxy</c> and the proxy environment
/// variables it reads; <see langword="null" /> for one that reads no variables, so only the
/// command line names a proxy.
/// </param>
/// <param name="connectionPool">
/// The run's connection pool, which <see cref="DisposeAsync" /> closes once the run ends
/// (ADR-0050), or <see langword="null" /> when the dispatcher's handlers keep none.
/// </param>
internal sealed class TransferDispatch(
    ProtocolDispatcher dispatcher,
    IReadOnlyList<string> warningLinesBeforeEachTransfer,
    CookieEngine? cookies = null,
    ProxySelector? proxySelector = null,
    IAsyncDisposable? connectionPool = null) : IAsyncDisposable
{
    /// <summary>
    /// Creates the dispatch with no warning lines.
    /// </summary>
    /// <param name="dispatcher">Performs each transfer with the handler for its scheme.</param>
    internal TransferDispatch(ProtocolDispatcher dispatcher)
        : this(dispatcher, [])
    {
    }

    /// <summary>Gets the dispatcher that performs each transfer with the handler for its scheme.</summary>
    internal ProtocolDispatcher Dispatcher { get; } = dispatcher;

    /// <summary>
    /// Gets the lines written to standard error before every URL's transfer unless <c>-s</c>
    /// is given; each without its line ending.
    /// </summary>
    internal IReadOnlyList<string> WarningLinesBeforeEachTransfer { get; } = warningLinesBeforeEachTransfer;

    /// <summary>
    /// Gets the run's cookies: the <c>-b</c> files to load before the first transfer and the
    /// <c>-c</c> jar to write after each HTTP transfer; <see langword="null" /> without <c>-b</c>
    /// or <c>-c</c>.
    /// </summary>
    internal CookieEngine? Cookies { get; } = cookies;

    /// <summary>
    /// Gets the selector that chooses each transfer's proxy from <c>-x</c>, <c>--noproxy</c> and
    /// the proxy environment variables it reads.
    /// </summary>
    internal ProxySelector ProxySelector { get; } = proxySelector ?? new ProxySelector(_ => null);

    /// <summary>
    /// Gets the run's connection pool, or <see langword="null" /> when the dispatcher's handlers
    /// keep none.
    /// </summary>
    internal IAsyncDisposable? ConnectionPool { get; } = connectionPool;

    /// <summary>
    /// Closes the run's <see cref="ConnectionPool" />, writing nothing, as curl closes its
    /// connection cache after the last transfer; does nothing without one.
    /// </summary>
    /// <returns>A task that completes when every idle connection is closed.</returns>
    public ValueTask DisposeAsync() => ConnectionPool?.DisposeAsync() ?? ValueTask.CompletedTask;
}
