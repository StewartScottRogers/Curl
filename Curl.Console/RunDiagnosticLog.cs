using System.Text;
using Curl.Core.FileSystem;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The one diagnostic log of a run, opened from its global <c>--log-level</c> and <c>--log-file</c>
/// (ADR-0222): <see cref="NoDiagnosticLog.Instance" /> at <c>none</c>, with no file created;
/// otherwise a <see cref="DiagnosticLogWriter" /> over standard error or over the <c>--log-file</c>,
/// created or truncated and written as UTF-8 without a byte order mark. Disposing it closes the file.
/// </summary>
internal sealed class RunDiagnosticLog : IAsyncDisposable
{
    /// <summary>
    /// What is written to standard error, before the path, when the <c>--log-file</c> cannot be
    /// opened (ADR-0222, decision 4).
    /// </summary>
    internal const string LogFileOpenFailedPrefix = "Warning: Failed to open the --log-file ";

    /// <summary>The <c>--log-file</c> writer the log owns, or <see langword="null" /> for none.</summary>
    private readonly StreamWriter? logFile;

    private RunDiagnosticLog(IDiagnosticLog log, StreamWriter? logFile, bool logFileOpenFailed)
    {
        Log = log;
        this.logFile = logFile;
        LogFileOpenFailed = logFileOpenFailed;
    }

    /// <summary>
    /// Gets the log every component of the run writes to.
    /// </summary>
    internal IDiagnosticLog Log { get; }

    /// <summary>
    /// Gets whether the <c>--log-file</c> could not be opened, so <see cref="Log" /> is
    /// <see cref="NoDiagnosticLog.Instance" /> and <see cref="LogFileOpenFailedPrefix" />'s warning is due.
    /// </summary>
    internal bool LogFileOpenFailed { get; }

    /// <summary>
    /// Opens the run's diagnostic log.
    /// </summary>
    /// <param name="level">The run's <c>--log-level</c>.</param>
    /// <param name="logFilePath">The run's <c>--log-file</c>, or <see langword="null" /> to log to standard error.</param>
    /// <param name="fileSystem">Opens the <c>--log-file</c>.</param>
    /// <param name="currentStandardError">Gets the stream standard error goes to at each write.</param>
    /// <param name="timeProvider">The clock each line's timestamp reads.</param>
    /// <param name="lineEnd">The line end the runner writes after its own standard-error lines.</param>
    /// <param name="gate">The run's write gate, which every line is written holding (<see cref="GatedDiagnosticLog" />).</param>
    /// <returns>The log.</returns>
    internal static async Task<RunDiagnosticLog> OpenAsync(
        DiagnosticLogLevel level,
        string? logFilePath,
        IFileSystem fileSystem,
        Func<Stream> currentStandardError,
        TimeProvider timeProvider,
        string lineEnd,
        WriteGate gate)
    {
        if (level == DiagnosticLogLevel.None)
        {
            return new(NoDiagnosticLog.Instance, null, logFileOpenFailed: false);
        }

        if (logFilePath is null)
        {
            return new(new GatedDiagnosticLog(new DiagnosticLogWriter(new StandardErrorLogTarget(currentStandardError), level, timeProvider, lineEnd), gate), null, logFileOpenFailed: false);
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(logFilePath, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } file)
        {
            return new(NoDiagnosticLog.Instance, null, logFileOpenFailed: true);
        }

        StreamWriter logFile = new(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new(new GatedDiagnosticLog(new DiagnosticLogWriter(logFile, level, timeProvider, lineEnd), gate), logFile, logFileOpenFailed: false);
    }

    /// <summary>
    /// Closes the <c>--log-file</c>, if one is open.
    /// </summary>
    /// <returns>A task that completes when the file is closed.</returns>
    public ValueTask DisposeAsync() => logFile?.DisposeAsync() ?? ValueTask.CompletedTask;
}
