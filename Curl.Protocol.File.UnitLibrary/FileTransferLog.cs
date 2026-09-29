using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File;

/// <summary>
/// Writes a <c>file://</c> transfer's steps to Curl's diagnostic log under the <c>file</c>
/// component (ADR-0222): the path opened and its size, or the open's failure, and the
/// transfer's end - its bytes and elapsed milliseconds at <c>info</c>, or its
/// <see cref="CurlExitCode" /> and message at <c>error</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class FileTransferLog(IDiagnosticLog diagnosticLog)
{
    /// <summary>Writes the source opened for a download and its size, at <c>info</c>.</summary>
    /// <param name="path">The operating system path opened.</param>
    /// <param name="length">The file's length.</param>
    public void OpenedForReading(string path, long length)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"opened {path} for reading: {length.ToString(CultureInfo.InvariantCulture)} bytes");
        }
    }

    /// <summary>Writes the destination opened for an upload and how, at <c>info</c>.</summary>
    /// <param name="path">The operating system path opened.</param>
    /// <param name="mode">Whether it was truncated or appended to.</param>
    public void OpenedForWriting(string path, FileWriteMode mode)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"opened {path} for writing: {mode}");
        }
    }

    /// <summary>Writes an open that failed and the file system's reason, at <c>error</c>.</summary>
    /// <param name="path">The operating system path the open was refused for.</param>
    /// <param name="purpose">What it was opened for: <c>reading</c> or <c>writing</c>.</param>
    /// <param name="status">Why the file system refused it.</param>
    public void OpenFailed(string path, string purpose, FileAccessStatus status)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"could not open {path} for {purpose}: {status}");
        }
    }

    /// <summary>
    /// Writes the transfer's end: its bytes and elapsed milliseconds at <c>info</c> when it
    /// succeeded, otherwise its <see cref="CurlExitCode" /> and message at <c>error</c>.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void Ended(TransferResult result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
        {
            Done(result.BytesTransferred, elapsed);
        }
        else
        {
            Failed(result);
        }
    }

    private void Done(long bytes, TimeSpan elapsed)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"transfer done: {bytes.ToString(CultureInfo.InvariantCulture)} bytes in {((long)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");
        }
    }

    private void Failed(TransferResult result)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"transfer failed with {result.ExitCode} (exit {((int)result.ExitCode).ToString(CultureInfo.InvariantCulture)}): {result.ErrorMessage}");
        }
    }

    private void Write(DiagnosticLogLevel level, string message) =>
        diagnosticLog.Write(level, DiagnosticLogComponents.File, message);
}
