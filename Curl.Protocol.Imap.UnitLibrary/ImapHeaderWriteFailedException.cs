namespace Curl.Protocol.Imap;

/// <summary>
/// Ends an <see cref="ImapSession" /> when the <c>-D</c> stream refuses a response line, which
/// the session reports as exit 23 without sending anything more, <c>LOGOUT</c> included, as
/// curl 8.21.0 does (BL-1138).
/// </summary>
/// <param name="lineLength">The length of the refused line, its line end included.</param>
internal sealed class ImapHeaderWriteFailedException(int lineLength) : Exception(ImapSessionMessages.HeaderWriteFailed(lineLength));
