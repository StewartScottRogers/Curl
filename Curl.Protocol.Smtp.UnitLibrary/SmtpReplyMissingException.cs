namespace Curl.Protocol.Smtp;

/// <summary>
/// Ends an <see cref="SmtpSession" /> when the server closes the connection before a reply
/// is complete, which the session reports as exit 56.
/// </summary>
internal sealed class SmtpReplyMissingException() : Exception(SmtpSessionMessages.ResponseReadingFailed);
