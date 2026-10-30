using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <content>The transfer's start and end and the failure that ends a session (BL-4366).</content>
internal sealed partial class SshDiagnosticLog
{
    /// <summary>Logs, at <c>info</c>, that the transfer's bytes start to move.</summary>
    /// <param name="scheme"><c>scp</c> or <c>sftp</c>.</param>
    /// <param name="path">The URL's path.</param>
    /// <param name="isUpload">Whether the transfer sends a file.</param>
    internal void TransferStarted(string scheme, string path, bool isUpload)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"{scheme} {(isUpload ? "upload to" : "download of")} {path} started");
        }
    }

    /// <summary>
    /// Logs how the transfer ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    internal void TransferEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            if (log.IsEnabled(DiagnosticLogLevel.Error))
            {
                Write(DiagnosticLogLevel.Error, FailureText(result.ExitCode, result.ErrorMessage));
            }
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"transfer finished: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>
    /// Logs, at <c>error</c>, a failure that ends the session with its <see cref="CurlExitCode" />
    /// and, when it came from libssh2's session startup, the libssh2 code it maps from.
    /// </summary>
    /// <param name="exception">The failure.</param>
    internal void Failed(SshTransferException exception)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            string text = FailureText(exception.ExitCode, exception.Message);
            Write(DiagnosticLogLevel.Error, exception.Libssh2Code is { } code
                ? string.Create(CultureInfo.InvariantCulture, $"{text} (libssh2 {code})")
                : text);
        }
    }

    private static string FailureText(CurlExitCode exitCode, string? message) =>
        string.Create(CultureInfo.InvariantCulture, $"failed with {exitCode} ({(int)exitCode}): {message}");
}
