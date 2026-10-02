namespace Curl.Protocol.Ftp;

/// <summary>
/// Thrown by <see cref="FtpControlChannel.ReadReplyAsync" /> for a control-connection reply
/// line holding a NUL byte, which curl 8.21.0 refuses with exit 8 before <c>-v</c> sees the
/// line (BL-1117).
/// </summary>
internal sealed class FtpReplyNulByteException()
    : Exception(FtpTransferMessages.NulByteInReplyLine);
