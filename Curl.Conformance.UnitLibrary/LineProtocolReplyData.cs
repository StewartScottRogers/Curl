using System.Text;

namespace Curl.Conformance;

/// <summary>
/// The reply data upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) picks with
/// <c>getreplydata</c> for a name such as a client name or a mailbox.
/// </summary>
internal static class LineProtocolReplyData
{
    /// <summary>
    /// Picks the reply data for a name: its number with any leading non-digits removed,
    /// <c>&lt;dataN&gt;</c> for a number over 10000 whose last four digits are N, else (or when
    /// that part is empty) <c>&lt;data&gt;</c>.
    /// </summary>
    /// <param name="replyParts">The case's <c>&lt;reply&gt;</c> parts by name.</param>
    /// <param name="name">The name; <see langword="null"/> counts as empty.</param>
    /// <returns>The part's text, decoded as Latin-1; empty when there is no such part.</returns>
    public static string Select(IReadOnlyDictionary<string, byte[]> replyParts, string? name)
    {
        string digits = new([.. (name ?? string.Empty).SkipWhile(character => !char.IsAsciiDigit(character)).TakeWhile(char.IsAsciiDigit)]);
        long number = long.TryParse(digits, out long parsed) ? parsed : 0;
        if (number > 10000 && replyParts.TryGetValue("data" + (number % 10000), out byte[]? numbered) && numbered.Length > 0)
        {
            return Encoding.Latin1.GetString(numbered);
        }

        return replyParts.TryGetValue("data", out byte[]? data) ? Encoding.Latin1.GetString(data) : string.Empty;
    }
}
