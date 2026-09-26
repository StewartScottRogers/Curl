using Curl.Core;

namespace Curl.Console;

/// <summary>
/// What <see cref="CurlCommandRunner" /> transfers through for one run: the dispatcher, and
/// the warning lines curl prints before each transfer for options the build ignores.
/// </summary>
/// <param name="dispatcher">Performs each transfer with the handler for its scheme.</param>
/// <param name="warningLinesBeforeEachTransfer">
/// The lines written to standard error before every URL's transfer unless <c>-s</c> is given,
/// such as the Schannel build's two <c>--capath</c> lines (ADR-0009); each without its line
/// ending. Empty when no option raises one.
/// </param>
internal sealed class TransferDispatch(
    ProtocolDispatcher dispatcher,
    IReadOnlyList<string> warningLinesBeforeEachTransfer)
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
}
