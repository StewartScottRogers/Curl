namespace Curl.Protocol.Pop3;

/// <summary>
/// Ends a <see cref="Pop3Session" /> when the server closes the connection before a
/// response is complete, which the session reports as exit 56.
/// </summary>
internal sealed class Pop3ReplyMissingException() : Exception(Pop3SessionMessages.ResponseReadingFailed);
