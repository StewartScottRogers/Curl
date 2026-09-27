using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Ends an <see cref="FtpDownloadSession" /> when the control conversation itself fails,
/// carrying the result the transfer reports: exit 55 for a command that could not be
/// sent, exit 56 for a reply that never arrived, exit 100 for a reply line too long to hold.
/// </summary>
/// <param name="result">The failed result to report.</param>
internal sealed class FtpControlConversationFailedException(TransferResult result)
    : Exception(result.ErrorMessage)
{
    /// <summary>Gets the failed result to report.</summary>
    public TransferResult Result { get; } = result;
}
