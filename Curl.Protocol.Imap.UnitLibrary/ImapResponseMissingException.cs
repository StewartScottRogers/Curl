namespace Curl.Protocol.Imap;

/// <summary>
/// Ends an <see cref="ImapSession" /> when the server closes the connection before a
/// response is complete, which the session reports as exit 56.
/// </summary>
internal sealed class ImapResponseMissingException() : Exception(ImapSessionMessages.ResponseReadingFailed);
