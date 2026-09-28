namespace Curl.Protocol.Imap;

/// <summary>
/// One complete IMAP response: the untagged responses the command waiting for it was
/// interested in, and how its tagged completion ended.
/// </summary>
/// <param name="Status">How the tagged completion ended.</param>
/// <param name="Untagged">
/// Each untagged response the reader was asked to keep, in order, starting <c>* </c> and
/// without its final line end. A response that carried literals holds each literal's
/// bytes, as Latin-1, between the line announcing it (with its line end) and the rest of
/// the response.
/// </param>
internal sealed record ImapResponse(ImapResponseStatus Status, IReadOnlyList<string> Untagged);
