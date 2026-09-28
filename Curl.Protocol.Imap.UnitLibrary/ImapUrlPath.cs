using System.Text;

namespace Curl.Protocol.Imap;

/// <summary>
/// The mailbox and the <c>;NAME=VALUE</c> parameters of an IMAP URL's path (RFC 5092), read
/// as curl 8.21.0's <c>imap_parse_url_path</c> reads them and measured with
/// <c>Record-CurlExchange.ps1 -Imap</c> (BL-555).
/// </summary>
/// <remarks>
/// The mailbox runs from after the leading <c>/</c> over every character RFC 5092 allows in
/// a path (<c>bchar</c>), less one trailing <c>/</c>, and is percent-decoded; none is
/// <see langword="null" />. Each parameter follows a <c>;</c>: its name runs to the
/// <c>=</c>, its value over <c>bchar</c> characters, and both are percent-decoded. The names
/// <c>UIDVALIDITY</c>, <c>UID</c>, <c>MAILINDEX</c>, <c>SECTION</c> and <c>PARTIAL</c> are
/// matched in any case, and a value loses one trailing <c>/</c>. A parameter with no
/// <c>=</c>, an unknown or repeated name, a decoded byte below 0x20, or anything left over
/// after the parameters makes the path malformed.
/// </remarks>
/// <param name="Mailbox">The decoded mailbox, or <see langword="null" /> when the path names none.</param>
/// <param name="UidValidity">The <c>UIDVALIDITY</c> value, or <see langword="null" />.</param>
/// <param name="Uid">The <c>UID</c> value, or <see langword="null" />.</param>
/// <param name="MailIndex">The <c>MAILINDEX</c> value, or <see langword="null" />.</param>
/// <param name="Section">The <c>SECTION</c> value, or <see langword="null" />.</param>
/// <param name="Partial">The <c>PARTIAL</c> value, or <see langword="null" />.</param>
internal sealed record ImapUrlPath(
    string? Mailbox,
    string? UidValidity,
    string? Uid,
    string? MailIndex,
    string? Section,
    string? Partial)
{
    /// <summary>The characters besides letters and digits RFC 5092's <c>bchar</c> allows, as curl lists them.</summary>
    private const string BcharPunctuation = ":@/&=-._~!$'()*+,%";

    /// <summary>The parameter names curl knows, matched in any case.</summary>
    private static readonly HashSet<string> KnownNames =
        new(["UIDVALIDITY", "UID", "MAILINDEX", "SECTION", "PARTIAL"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads <paramref name="absolutePath" />, the URL's path as sent, not yet decoded.
    /// </summary>
    /// <param name="absolutePath">The path, starting with <c>/</c>.</param>
    /// <returns>The mailbox and parameters, or <see langword="null" /> when the path is malformed.</returns>
    public static ImapUrlPath? Parse(string absolutePath)
    {
        int end = BcharRunEnd(absolutePath, 1);
        if (!TryDecodeMailbox(absolutePath[1..end], out string? mailbox)
            || ParseParameters(absolutePath, end) is not { } parameters)
        {
            return null;
        }

        return new ImapUrlPath(
            mailbox,
            parameters.GetValueOrDefault("UIDVALIDITY"),
            parameters.GetValueOrDefault("UID"),
            parameters.GetValueOrDefault("MAILINDEX"),
            parameters.GetValueOrDefault("SECTION"),
            parameters.GetValueOrDefault("PARTIAL"));
    }

    /// <summary>
    /// Decodes the mailbox <paramref name="text" />, less one trailing <c>/</c>; none when
    /// the text is empty. <see langword="false" /> when it holds a control byte.
    /// </summary>
    private static bool TryDecodeMailbox(string text, out string? mailbox)
    {
        mailbox = text.Length == 0 ? null : Decode(WithoutTrailingSlash(text));
        return text.Length == 0 || mailbox is not null;
    }

    /// <summary>
    /// Reads every <c>;NAME=VALUE</c> parameter from <paramref name="start" /> to the end of
    /// <paramref name="text" />, keyed by name in any case; <see langword="null" /> when one
    /// is malformed or something else follows them.
    /// </summary>
    private static Dictionary<string, string>? ParseParameters(string text, int start)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int end = start;
        while (end < text.Length)
        {
            end = text[end] == ';' ? ParseParameter(text, end + 1, parameters) : -1;
            if (end < 0)
            {
                return null;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Adds the parameter starting at <paramref name="start" />, after its <c>;</c>, to
    /// <paramref name="parameters" />.
    /// </summary>
    /// <returns>Where the parameter ends, or -1 when it is malformed, unknown or repeated.</returns>
    private static int ParseParameter(string text, int start, Dictionary<string, string> parameters)
    {
        int equals = text.IndexOf('=', start);
        if (equals < 0)
        {
            return -1;
        }

        int end = BcharRunEnd(text, equals + 1);
        string? name = Decode(text[start..equals]);
        string? value = Decode(text[(equals + 1)..end]);
        return name is not null && value is not null && KnownNames.Contains(name)
            && parameters.TryAdd(name, WithoutTrailingSlash(value))
                ? end
                : -1;
    }

    private static string WithoutTrailingSlash(string text) => text.EndsWith('/') ? text[..^1] : text;

    /// <summary>Where the run of <c>bchar</c> characters starting at <paramref name="start" /> ends.</summary>
    private static int BcharRunEnd(string text, int start)
    {
        int end = start;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || BcharPunctuation.Contains(text[end])))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    /// Percent-decodes <paramref name="text" /> as curl does, keeping a <c>%</c> not followed
    /// by two hexadecimal digits; each decoded byte becomes one Latin-1 character, so the
    /// bytes go back on the wire as they came. <see langword="null" /> when a byte is below 0x20.
    /// </summary>
    private static string? Decode(string text)
    {
        var decoded = new StringBuilder(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char next = text[index];
            if (next == '%' && IsPercentEscapeAt(text, index))
            {
                next = (char)Convert.ToByte(text.Substring(index + 1, 2), 16);
                index += 2;
            }

            if (next < ' ')
            {
                return null;
            }

            decoded.Append(next);
        }

        return decoded.ToString();
    }

    /// <summary>Whether the <c>%</c> at <paramref name="index" /> is followed by two hexadecimal digits.</summary>
    private static bool IsPercentEscapeAt(string text, int index) =>
        index + 2 < text.Length && char.IsAsciiHexDigit(text[index + 1]) && char.IsAsciiHexDigit(text[index + 2]);
}
