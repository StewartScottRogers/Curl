namespace Curl.Protocol.Ftp;

/// <summary>
/// Thrown by <see cref="FtpControlChannel.ReadReplyAsync" /> when the <c>-D</c> stream
/// refused a control-connection reply line, which curl 8.21.0's header callback answers
/// with an error, ending the transfer with exit 23 (BL-1131).
/// </summary>
/// <param name="length">The length of the refused line, its line end included, which the message names.</param>
/// <param name="inner">The stream's <see cref="IOException" />.</param>
internal sealed class FtpReplyLineWriteException(int length, IOException inner)
    : Exception(FtpTransferMessages.HeaderWriteFailed(length), inner);
