using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File;

/// <summary>
/// The path half of a <c>file://</c> URL, in both of the forms curl needs it: as written
/// in the URL, and as handed to the operating system.
/// </summary>
/// <remarks>
/// <para>
/// The path is read from <see cref="CurlUrl.AbsolutePath" />, where
/// <see cref="CurlUrl" /> has already applied curl's <c>file://</c> rules to the text as
/// typed. The measurements behind those rules are recorded in ADR-0003 and ADR-0010
/// (<c>Documentation/Planning/Decisions</c>), taken against curl 8.21.0.
/// </para>
/// <para>
/// A URL <see cref="CurlUrl.TryParse(string, bool, out CurlUrl)" /> rejects never
/// reaches this type: the transfer ends with exit 3 before a handler runs.
/// </para>
/// </remarks>
public sealed record FileUrlPath
{
    /// <summary>
    /// Holds the two forms of an already parsed path.
    /// </summary>
    /// <param name="urlPath">The path as curl reports it; see <see cref="UrlPath" />.</param>
    /// <param name="osPath">The path handed to the operating system; see <see cref="OsPath" />.</param>
    private FileUrlPath(string urlPath, string osPath)
    {
        UrlPath = urlPath;
        OsPath = osPath;
    }

    /// <summary>
    /// The path as curl reports it, still percent-encoded: every <c>\</c> already turned into
    /// <c>/</c> and, unless <c>--path-as-is</c> is in force, its dot segments removed. This is
    /// the text curl echoes in its exit 37 message — <c>file:///C:/dir/../nosuch.txt</c> is
    /// quoted as <c>C:/nosuch.txt</c> — so it is built from the encoded text rather than
    /// reconstructed from the decoded form. Every well-formed <c>%xx</c> escape it keeps is
    /// written with uppercase hexadecimal digits, as curl 8.21.0 quotes it:
    /// <c>file:///C:/dir/a%2eb/x</c> is quoted as <c>C:/dir/a%2Eb/x</c>. A malformed escape
    /// — <c>%2</c>, <c>%GG</c>, <c>%g2</c>, a trailing <c>%</c> — is kept exactly as written.
    /// A non-ASCII character written unescaped is encoded as its UTF-8 bytes, so
    /// <c>file:///C:/dir/é</c> is quoted as <c>C:/dir/%C3%A9</c>; printable ASCII is never
    /// encoded.
    /// </summary>
    public string UrlPath { get; }

    /// <summary>
    /// The percent-decoded operating-system path handed to
    /// <see cref="Abstractions.IFileSystem" />: <c>UrlPath</c> decoded, with the platform's
    /// directory separators. It holds a <c>.</c> or <c>..</c> segment only when the
    /// <see cref="CurlUrl" /> was parsed with <c>pathAsIs</c>.
    /// </summary>
    public string OsPath { get; }

    /// <summary>
    /// The only scheme this type parses, compared ordinally and ignoring case.
    /// </summary>
    private const string FileScheme = "file";

    /// <summary>
    /// Extracts the URL path and the operating-system path from a <c>file://</c> URL.
    /// </summary>
    /// <param name="url">The URL to read. Its scheme must be <c>file</c>.</param>
    /// <param name="path">
    /// On success, the parsed pair; otherwise undefined and not to be read.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="url" /> is a <c>file://</c> URL;
    /// <see langword="false" /> for any other scheme, which the caller reports as
    /// <see cref="Abstractions.CurlExitCode.UrlMalformat" />.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The path comes from <see cref="CurlUrl.AbsolutePath" />, which has already applied
    /// curl's <c>file://</c> rules to the text as typed, so nothing here re-reads
    /// <see cref="CurlUrl.OriginalString" />. By then, as measured against curl 8.21.0
    /// (ADR-0003, ADR-0010):
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// the query and fragment are gone, and every <c>\</c> is a <c>/</c> —
    /// <c>file:///C:/dir\..\secret.txt</c> opens <c>C:/secret.txt</c> — while an escaped
    /// backslash, <c>%5C</c>, stays an escape;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// the authority is dropped when it is empty, <c>localhost</c> or <c>127.0.0.1</c>,
    /// and kept as the head of the path when it is a drive specification, so
    /// <c>file://C:/dir/x</c> and <c>file:///C:/dir/x</c> are the same path and
    /// <c>file://C:</c> is the path <c>C:</c>; <c>file:////server/share</c> keeps both
    /// leading slashes of <c>//server/share</c>;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// unless the URL was parsed with <c>pathAsIs</c> (<c>--path-as-is</c>), the dot
    /// segments are removed, spelled plainly or as <c>%2e</c>, and a drive followed by
    /// <c>/</c> or by nothing is a root they cannot climb above:
    /// <c>file:///C:/../../nosuch.txt</c> is quoted as <c>C:/nosuch.txt</c>, while
    /// <c>file:///Q:dir/../x</c> is quoted as <c>/x</c>.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// This method then takes three steps. <strong>Drive letters:</strong> on Windows, a
    /// path of the form <c>/X:…</c> or <c>/X|…</c>, where <c>X</c> is a single ASCII
    /// letter, loses its leading slash, so <c>file:///c:/Windows/win.ini</c> becomes an
    /// absolute Windows path and <c>file:///C:%2FWindows/win.ini</c> opens
    /// <c>C:\Windows\win.ini</c>, as curl 8.21.0 does; the <c>|</c> spelling is kept,
    /// because curl does not translate it. On every other platform the slash stays, so
    /// the same URL opens the absolute path <c>/C:/Windows/win.ini</c>, because curl
    /// 8.21.0 strips it only inside <c>#ifdef DOS_FILESYSTEM</c> in <c>lib/file.c</c>.
    /// <strong>Quote:</strong> <c>UrlPath</c> is that text with each unescaped non-ASCII
    /// character encoded as its UTF-8 bytes and the hexadecimal digits of each well-formed
    /// escape uppercased, a malformed one left as written. <strong>Decode:</strong>
    /// <c>OsPath</c> is that text with each <c>%XX</c> escape decoded to the byte it names
    /// and runs of escapes read as UTF-8, <c>%2F</c> included, a malformed escape left as
    /// written, and <c>/</c> turned into the platform's directory separator; no trailing
    /// separator is trimmed and no case is changed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url" /> is <see langword="null" />.
    /// </exception>
    public static bool TryParse(CurlUrl url, [MaybeNullWhen(false)] out FileUrlPath path) =>
        TryParse(url, OperatingSystem.IsWindows(), out path);

    /// <summary>
    /// Extracts the URL path and the operating-system path from a <c>file://</c> URL with
    /// the drive-letter rule of the chosen platform.
    /// </summary>
    /// <param name="url">The URL to read. Its scheme must be <c>file</c>.</param>
    /// <param name="driveLetters">
    /// <see langword="true" /> for the Windows rule, which drops the slash in front of a
    /// <c>/X:…</c> or <c>/X|…</c> drive; <see langword="false" /> for every other
    /// platform, which keeps it.
    /// </param>
    /// <param name="path">
    /// On success, the parsed pair; otherwise undefined and not to be read.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="url" /> is a <c>file://</c> URL.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url" /> is <see langword="null" />.
    /// </exception>
    internal static bool TryParse(
        CurlUrl url,
        bool driveLetters,
        [MaybeNullWhen(false)] out FileUrlPath path)
    {
        ArgumentNullException.ThrowIfNull(url);

        path = null;

        if (!string.Equals(url.Scheme, FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // CurlUrl reads a scheme only before ":/", so the path is never empty here.
        string urlPath = driveLetters
            ? StripDriveLetterSlash(url.AbsolutePath)
            : url.AbsolutePath;

        path = new FileUrlPath(
            UppercaseEscapes(EncodeNonAscii(urlPath)),
            ToOperatingSystemPath(urlPath));

        return true;
    }

    /// <summary>
    /// Drops the slash in front of a <c>/X:…</c> or <c>/X|…</c> drive specification.
    /// </summary>
    /// <param name="urlPath">The still-encoded path.</param>
    /// <returns>The path curl would hand on.</returns>
    private static string StripDriveLetterSlash(string urlPath) =>
        urlPath.Length >= 3
        && urlPath[0] == '/'
        && char.IsAsciiLetter(urlPath[1])
        && (urlPath[2] == ':' || urlPath[2] == '|')
            ? urlPath[1..]
            : urlPath;

    /// <summary>
    /// Percent-encodes every non-ASCII character as its UTF-8 bytes, leaving ASCII,
    /// escapes included, exactly as written.
    /// </summary>
    /// <param name="urlPath">The still-encoded path.</param>
    /// <returns>The path with nothing but ASCII in it.</returns>
    /// <remarks>
    /// Measured against the Unicode build of curl 8.21.0, which receives its arguments as
    /// UTF-16 as .NET does: <c>file:///C:/nodir/aéb</c> is quoted as
    /// <c>C:/nodir/a%C3%A9b</c>, and a character outside code page 1252 such as
    /// <c>日</c> as <c>%E6%97%A5</c>. A lone surrogate is encoded as U+FFFD.
    /// </remarks>
    private static string EncodeNonAscii(string urlPath)
    {
        var encoded = new StringBuilder(urlPath.Length);
        Span<byte> utf8 = stackalloc byte[4];

        foreach (Rune rune in urlPath.EnumerateRunes())
        {
            if (rune.IsAscii)
            {
                encoded.Append((char)rune.Value);
                continue;
            }

            int length = rune.EncodeToUtf8(utf8);

            foreach (byte value in utf8[..length])
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Rewrites the hexadecimal digits of every well-formed <c>%xx</c> escape in uppercase,
    /// leaving a malformed escape and every other character exactly as written.
    /// </summary>
    /// <param name="urlPath">The still-encoded path.</param>
    /// <returns>The path as curl 8.21.0 quotes it in its exit 37 message.</returns>
    private static string UppercaseEscapes(string urlPath)
    {
        var quoted = new StringBuilder(urlPath);

        for (int index = 0; index < quoted.Length; index++)
        {
            if (TryReadEscape(urlPath, index, out _))
            {
                quoted[index + 1] = char.ToUpperInvariant(quoted[index + 1]);
                quoted[index + 2] = char.ToUpperInvariant(quoted[index + 2]);
                index += 2;
            }
        }

        return quoted.ToString();
    }

    /// <summary>
    /// Decodes the escapes and switches to the platform's directory separator.
    /// </summary>
    /// <param name="urlPath">The still-encoded path.</param>
    /// <returns>The path to hand to <see cref="Abstractions.IFileSystem" />.</returns>
    private static string ToOperatingSystemPath(string urlPath) =>
        Decode(urlPath).Replace('/', Path.DirectorySeparatorChar);

    /// <summary>
    /// Percent-decodes <paramref name="encoded" />, reading each run of escapes as UTF-8
    /// and leaving a malformed escape exactly as written.
    /// </summary>
    /// <param name="encoded">The still-encoded path.</param>
    /// <returns>The decoded text.</returns>
    private static string Decode(string encoded)
    {
        if (!encoded.Contains('%', StringComparison.Ordinal))
        {
            return encoded;
        }

        var decoded = new StringBuilder(encoded.Length);
        var escaped = new List<byte>(4);
        int index = 0;

        while (index < encoded.Length)
        {
            if (TryReadEscape(encoded, index, out byte value))
            {
                escaped.Add(value);
                index += 3;
                continue;
            }

            Flush(escaped, decoded);
            decoded.Append(encoded[index]);
            index++;
        }

        Flush(escaped, decoded);

        return decoded.ToString();
    }

    /// <summary>
    /// Reads the <c>%XX</c> escape at <paramref name="index" />, if there is one.
    /// </summary>
    /// <param name="text">The still-encoded path.</param>
    /// <param name="index">Where to look.</param>
    /// <param name="value">On success, the byte the escape names.</param>
    /// <returns>
    /// <see langword="false" /> when the text at <paramref name="index" /> is not a
    /// complete escape with two hexadecimal digits.
    /// </returns>
    private static bool TryReadEscape(string text, int index, out byte value)
    {
        value = 0;

        if (text[index] != '%' || index + 2 >= text.Length)
        {
            return false;
        }

        char high = text[index + 1];
        char low = text[index + 2];

        if (!char.IsAsciiHexDigit(high) || !char.IsAsciiHexDigit(low))
        {
            return false;
        }

        value = (byte)((HexValue(high) << 4) | HexValue(low));

        return true;
    }

    /// <summary>
    /// Converts one hexadecimal digit to its value.
    /// </summary>
    /// <param name="digit">An ASCII hexadecimal digit in either case.</param>
    /// <returns>The value zero to fifteen.</returns>
    private static int HexValue(char digit) =>
        char.IsAsciiDigit(digit) ? digit - '0' : ((digit | 0x20) - 'a') + 10;

    /// <summary>
    /// Appends the bytes gathered so far as UTF-8 text and empties the buffer.
    /// </summary>
    /// <param name="escaped">The bytes from a run of consecutive escapes.</param>
    /// <param name="decoded">The text being built.</param>
    private static void Flush(List<byte> escaped, StringBuilder decoded)
    {
        if (escaped.Count == 0)
        {
            return;
        }

        decoded.Append(Encoding.UTF8.GetString(escaped.ToArray()));
        escaped.Clear();
    }
}
