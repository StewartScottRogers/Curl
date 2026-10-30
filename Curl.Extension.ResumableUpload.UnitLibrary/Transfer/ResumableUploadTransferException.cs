using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Ends a resumable upload's exchange with curl's exit code for the failure, saying whether the upload may resume after it.</summary>
/// <param name="exitCode">Curl's exit code for the failure.</param>
/// <param name="message">The message curl prints.</param>
/// <param name="canResume">Whether the connection broke, so the upload may retrieve the server's offset and resume.</param>
internal sealed class ResumableUploadTransferException(CurlExitCode exitCode, string message, bool canResume) : Exception(message)
{
    /// <summary>Gets curl's exit code for the failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Gets a value indicating whether the connection broke, so the upload may resume.</summary>
    public bool CanResume { get; } = canResume;
}
