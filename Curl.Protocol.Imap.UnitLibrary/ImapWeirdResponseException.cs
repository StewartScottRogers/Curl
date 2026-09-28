namespace Curl.Protocol.Imap;

/// <summary>
/// Ends an <see cref="ImapSession" /> when a response line is one curl refuses outright - a
/// continuation no command asked for, or a line holding a NUL byte - which the session
/// reports as exit 8 with <paramref name="message" />.
/// </summary>
/// <param name="message">What curl prints for the line, such as <c>Unexpected continuation response</c>.</param>
internal sealed class ImapWeirdResponseException(string message) : Exception(message);
