using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Maps the error code of a TFTP ERROR packet (RFC 1350 section 5, RFC 2347 for code 8)
/// to the curl exit code and message curl 8.21.0 reports for it.
/// </summary>
/// <remarks>
/// Codes 0 to 8 were measured against curl 8.21.0. The messages are the
/// <c>curl_easy_strerror</c> text of each exit code, not the message the server put in
/// its packet, which curl does not report. Code 0 ("not defined") and code 4 ("illegal
/// operation") share exit 71; code 8 (option negotiation refused) and any code outside
/// 0 to 7 fall through curl's <c>switch</c> in <c>lib/tftp.c</c> to exit 42.
/// </remarks>
internal static class TftpErrorMapping
{
    /// <summary>
    /// Gets the curl exit code and message for a TFTP error code.
    /// </summary>
    /// <param name="tftpErrorCode">The error code from the ERROR packet.</param>
    /// <returns>The failed transfer result curl reports for it.</returns>
    internal static TransferResult ToTransferResult(ushort tftpErrorCode) => tftpErrorCode switch
    {
        0 or 4 => TransferResult.Failure(CurlExitCode.TftpIllegal, "TFTP: Illegal operation"),
        1 => TransferResult.Failure(CurlExitCode.TftpNotFound, "TFTP: File Not Found"),
        2 => TransferResult.Failure(CurlExitCode.TftpPerm, "TFTP: Access Violation"),
        3 => TransferResult.Failure(CurlExitCode.RemoteDiskFull, "Disk full or allocation exceeded"),
        5 => TransferResult.Failure(CurlExitCode.TftpUnknownId, "TFTP: Unknown transfer ID"),
        6 => TransferResult.Failure(CurlExitCode.RemoteFileExists, "Remote file already exists"),
        7 => TransferResult.Failure(CurlExitCode.TftpNoSuchUser, "TFTP: No such user"),
        _ => TransferResult.Failure(
            CurlExitCode.AbortedByCallback,
            "Operation was aborted by an application callback"),
    };
}
