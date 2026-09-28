using Curl.Cli;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Where one run's <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> output goes: the
/// <see cref="ITransferEvents" /> every transfer of the run reports to, and the trace file it
/// writes, which this owns and closes when disposed.
/// </summary>
/// <param name="events">The sink every transfer of the run reports to.</param>
/// <param name="ownedTraceFile">The trace file opened for the run, or <see langword="null" /> when none was.</param>
/// <remarks>
/// As curl 8.21.0 does (measured 2026-09-27, BL-242 Notes): <c>-v</c> writes to standard error;
/// a <c>--trace</c> or <c>--trace-ascii</c> file is opened once per run, truncated, and every
/// transfer's dump goes into it; <c>-</c> names standard output and <c>%</c> standard error; a
/// file that cannot be opened sends the dump to standard error, with no warning. Each is in text
/// mode on Windows, so every line ends CR LF there.
/// </remarks>
internal sealed class TransferEventOutput(ITransferEvents events, Stream? ownedTraceFile) : IAsyncDisposable
{
    /// <summary>The <c>--trace</c> file name that means standard output.</summary>
    internal const string StandardOutputTraceFile = "-";

    /// <summary>The <c>--trace</c> file name that means standard error.</summary>
    internal const string StandardErrorTraceFile = "%";

    /// <summary>
    /// The output of a run with no <c>-v</c>, <c>--trace</c> or <c>--trace-ascii</c>: events go nowhere.
    /// </summary>
    internal static readonly TransferEventOutput None = new(NoTransferEvents.Instance, null);

    /// <summary>
    /// Gets the sink every transfer of the run reports to; <see cref="NoTransferEvents.Instance" />
    /// when nothing is shown.
    /// </summary>
    internal ITransferEvents Events { get; } = events;

    /// <summary>
    /// Opens the output <paramref name="options" /> ask for.
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="fileSystem">Opens the trace file.</param>
    /// <param name="standardOutput">Where a <c>--trace -</c> dump goes.</param>
    /// <param name="standardError">Where <c>-v</c> lines, a <c>--trace %</c> dump and a dump whose file cannot be opened go.</param>
    /// <param name="runsOnWindows">Whether each line feed is written as CR LF, as curl's text-mode streams do on Windows.</param>
    /// <param name="standardOutputIsTerminal">
    /// Whether standard output is a terminal, where curl's <c>-v</c> shows no <c>[N bytes data]</c> lines.
    /// </param>
    /// <param name="timeProvider">The clock a <c>--trace-time</c> stamp on a <c>-v</c> line or a dump reads.</param>
    /// <returns>The output; <see cref="None" /> when no trace option is in effect.</returns>
    internal static async Task<TransferEventOutput> OpenAsync(
        CommandLineOptions options,
        IFileSystem fileSystem,
        Stream standardOutput,
        Stream standardError,
        bool runsOnWindows,
        bool standardOutputIsTerminal,
        TimeProvider timeProvider)
    {
        if (options.Trace == TraceKind.None)
        {
            return None;
        }

        if (options.Trace == TraceKind.Verbose)
        {
            return new TransferEventOutput(
                new VerboseTransferEventWriter(
                    TextMode(standardError, runsOnWindows),
                    !standardOutputIsTerminal,
                    options.TraceTime,
                    timeProvider,
                    PlatformTlsBackend.ForProcess),
                null);
        }

        (Stream target, Stream? ownedFile) = await OpenTraceTargetAsync(options.TraceFile!, fileSystem, standardOutput, standardError)
            .ConfigureAwait(false);
        TraceDumpFormat format = options.Trace == TraceKind.HexDump ? TraceDumpFormat.HexAndText : TraceDumpFormat.TextOnly;

        return new TransferEventOutput(
            new TraceTransferEventWriter(TextMode(target, runsOnWindows), format, options.TraceTime, timeProvider),
            ownedFile);
    }

    /// <summary>
    /// Closes the trace file, when the run opened one.
    /// </summary>
    /// <returns>A task that completes when the file is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (ownedTraceFile is not null)
        {
            await ownedTraceFile.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens where a dump goes: standard output for <c>-</c>, standard error for <c>%</c>, else the
    /// named file, truncated, or standard error when it cannot be opened.
    /// </summary>
    /// <param name="traceFile">The <c>--trace</c> or <c>--trace-ascii</c> file name.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="standardOutput">Standard output.</param>
    /// <param name="standardError">Standard error.</param>
    /// <returns>The stream to write to, and the file to close afterwards when one was opened.</returns>
    private static async Task<(Stream Target, Stream? OwnedFile)> OpenTraceTargetAsync(
        string traceFile,
        IFileSystem fileSystem,
        Stream standardOutput,
        Stream standardError)
    {
        switch (traceFile)
        {
            case StandardOutputTraceFile:
                return (standardOutput, null);
            case StandardErrorTraceFile:
                return (standardError, null);
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(traceFile, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);

        return opened.Content is { } file ? (file, file) : (standardError, null);
    }

    /// <summary>
    /// Wraps <paramref name="stream" /> so each line feed is written as CR LF on Windows.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>The stream to write lines to.</returns>
    private static Stream TextMode(Stream stream, bool runsOnWindows) =>
        runsOnWindows ? new LineFeedToCrLfStream(stream) : stream;
}
