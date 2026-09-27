using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict;

/// <summary>
/// Encodes a <c>dict</c> URL's path as the bytes curl 8.21.0 sends: a <c>CLIENT</c> line,
/// one command line, and <c>QUIT</c>, each ending in CRLF.
/// </summary>
/// <remarks>
/// <para>
/// The path is percent-decoded first, so an encoded <c>%3A</c> separates fields as
/// <c>:</c> does. A path starting <c>/m:</c>, <c>/match:</c> or <c>/find:</c> becomes
/// <c>MATCH database strategy word</c>; one starting <c>/d:</c>, <c>/define:</c> or
/// <c>/lookup:</c> becomes <c>DEFINE database word</c>, the prefixes compared without
/// regard to case. The fields after the prefix are word, database, then strategy (for
/// <c>MATCH</c>), separated by <c>:</c>, and any further fields are ignored. An empty or
/// missing word is <c>default</c>, database <c>!</c>, strategy <c>.</c>. Each byte of the
/// word that is at most <c>0x20</c>, at least <c>0x7F</c>, or one of <c>'</c>, <c>"</c>
/// and <c>\</c> is sent with a backslash before it; database and strategy are sent as
/// decoded.
/// </para>
/// <para>
/// Any other path is sent as the command itself, without its leading <c>/</c> and with
/// each <c>:</c> replaced by a space. A decoded path holding a byte below <c>0x20</c> is
/// refused, as curl refuses it with exit 3.
/// </para>
/// </remarks>
internal static class DictRequest
{
    /// <summary>
    /// The line that names the client to the server. It names upstream curl's own version
    /// so the bytes sent match curl 8.21.0; it is one constant so it can follow the version
    /// this project measures against.
    /// </summary>
    public const string ClientLine = "CLIENT libcurl 8.21.0";

    private const string QuitLine = "QUIT";

    private const string DefaultWord = "default";

    private const string AnyDatabase = "!";

    private const string DefaultStrategy = ".";

    private static readonly string[] MatchPrefixes = ["/MATCH:", "/M:", "/FIND:"];

    private static readonly string[] DefinePrefixes = ["/DEFINE:", "/D:", "/LOOKUP:"];

    /// <summary>
    /// Encodes the request for <paramref name="escapedPath" />, or refuses a path that
    /// decodes to a control character.
    /// </summary>
    /// <param name="escapedPath">
    /// The URL's path as <see cref="CurlUrl.AbsolutePath" /> gives it: as written, still
    /// percent-encoded.
    /// </param>
    /// <param name="request">The whole request, or empty when refused.</param>
    /// <returns><see langword="true" /> unless the decoded path holds a byte below <c>0x20</c>.</returns>
    public static bool TryEncode(string escapedPath, out byte[] request)
    {
        ArgumentNullException.ThrowIfNull(escapedPath);

        byte[] path = PercentDecode(escapedPath);
        if (path.Any(static value => value < 0x20))
        {
            request = [];
            return false;
        }

        request =
        [
            .. Line(Encoding.ASCII.GetBytes(ClientLine)),
            .. Line(EncodeCommand(path)),
            .. Line(Encoding.ASCII.GetBytes(QuitLine)),
        ];
        return true;
    }

    private static byte[] Line(byte[] text) => [.. text, (byte)'\r', (byte)'\n'];

    private static byte[] EncodeCommand(byte[] path)
    {
        if (StartsWithAny(path, MatchPrefixes))
        {
            byte[][] fields = Fields(path);
            return Join("MATCH"u8.ToArray(), OrDefault(fields, 1, AnyDatabase), OrDefault(fields, 2, DefaultStrategy), Word(fields));
        }

        if (StartsWithAny(path, DefinePrefixes))
        {
            byte[][] fields = Fields(path);
            return Join("DEFINE"u8.ToArray(), OrDefault(fields, 1, AnyDatabase), Word(fields));
        }

        return [.. path.Skip(1).Select(static value => value == (byte)':' ? (byte)' ' : value)];
    }

    private static bool StartsWithAny(byte[] path, string[] prefixes) =>
        prefixes.Any(prefix => path.Length >= prefix.Length
            && Encoding.ASCII.GetString(path, 0, prefix.Length).Equals(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Splits what follows the prefix's colon into at most three fields.</summary>
    private static byte[][] Fields(byte[] path)
    {
        int start = Array.IndexOf(path, (byte)':') + 1;
        return [.. SplitOnColons(path[start..]).Take(3)];
    }

    private static IEnumerable<byte[]> SplitOnColons(byte[] text)
    {
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == (byte)':')
            {
                yield return text[start..index];
                start = index + 1;
            }
        }

        yield return text[start..];
    }

    private static byte[] OrDefault(byte[][] fields, int index, string fallback) =>
        index < fields.Length && fields[index].Length > 0 ? fields[index] : Encoding.ASCII.GetBytes(fallback);

    private static byte[] Word(byte[][] fields) =>
        fields[0].Length == 0 ? Encoding.ASCII.GetBytes(DefaultWord) : EscapeWord(fields[0]);

    private static byte[] EscapeWord(byte[] word)
    {
        var escaped = new List<byte>(word.Length * 2);
        foreach (byte value in word)
        {
            if (NeedsBackslash(value))
            {
                escaped.Add((byte)'\\');
            }

            escaped.Add(value);
        }

        return [.. escaped];
    }

    private static bool NeedsBackslash(byte value) =>
        value <= 0x20 || value >= 0x7F || value is (byte)'\'' or (byte)'"' or (byte)'\\';

    private static byte[] Join(params byte[][] parts)
    {
        var joined = new List<byte>(parts[0]);
        foreach (byte[] part in parts.Skip(1))
        {
            joined.Add((byte)' ');
            joined.AddRange(part);
        }

        return [.. joined];
    }

    /// <summary>
    /// Decodes each <c>%</c> followed by two hexadecimal digits to one byte and copies
    /// every other character as its UTF-8 bytes, so a stray <c>%</c> stays as it is, as
    /// curl leaves one.
    /// </summary>
    private static byte[] PercentDecode(string escaped)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(escaped);
        var decoded = new List<byte>(encoded.Length);
        int index = 0;
        while (index < encoded.Length)
        {
            decoded.Add(DecodeAt(encoded, ref index));
        }

        return [.. decoded];
    }

    private static byte DecodeAt(byte[] encoded, ref int index)
    {
        if (encoded[index] == '%'
            && index + 2 < encoded.Length
            && byte.TryParse(encoded.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte escaped))
        {
            index += 3;
            return escaped;
        }

        return encoded[index++];
    }
}
