using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Curl.Protocol.File;

/// <summary>
/// The path half of a <c>file://</c> URL, in both of the forms curl needs it: as written
/// in the URL, and as handed to the operating system.
/// </summary>
/// <remarks>
/// <para>
/// Parsing works from <see cref="Uri.OriginalString" />, never
/// <see cref="Uri.AbsolutePath" /> and never <see cref="Uri.LocalPath" />. Those two
/// rewrite <c>c|</c> to <c>c:</c> and fold <c>file:////server/share</c> into a UNC
/// authority, neither of which curl does, and they remove <c>..</c> always, where curl
/// removes it only without <c>--path-as-is</c>. The
/// measurements behind that are recorded in ADR-0003
/// (<c>Documentation/Planning/Decisions</c>), taken against curl 8.21.0.
/// </para>
/// <para>
/// The wider question of URLs <see cref="Uri" /> cannot round-trip at all — a
/// <c>%2F</c> in the drive position, a userinfo component — is task BL-010; those URLs
/// never reach this type, because <see cref="Uri" /> throws before it is called.
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
    /// directory separators. It holds a <c>.</c> or <c>..</c> segment only when the URL was
    /// parsed with <c>pathAsIs</c>.
    /// </summary>
    public string OsPath { get; }

    /// <summary>
    /// The only scheme this type parses, compared ordinally and ignoring case.
    /// </summary>
    private const string FileScheme = "file";

    /// <summary>
    /// The two characters that end the path and begin something this type discards.
    /// </summary>
    private static readonly char[] QueryOrFragment = ['?', '#'];

    /// <summary>
    /// Extracts the URL path and the operating-system path from a <c>file://</c> URL,
    /// removing its dot segments as curl does without <c>--path-as-is</c>.
    /// </summary>
    /// <param name="url">The URL to read. Its scheme must be <c>file</c>.</param>
    /// <param name="path">
    /// On success, the parsed pair; otherwise undefined and not to be read.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="url" /> is a <c>file://</c> URL whose
    /// path curl would accept; <see langword="false" /> when the caller should report
    /// <see cref="Abstractions.CurlExitCode.UrlMalformat" />.
    /// </returns>
    /// <remarks>
    /// The same as <see cref="TryParse(Uri, bool, out FileUrlPath)" /> with
    /// <c>pathAsIs</c> <see langword="false" />.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url" /> is <see langword="null" />.
    /// </exception>
    public static bool TryParse(Uri url, [MaybeNullWhen(false)] out FileUrlPath path) =>
        TryParse(url, pathAsIs: false, out path);

    /// <summary>
    /// Extracts the URL path and the operating-system path from a <c>file://</c> URL.
    /// </summary>
    /// <param name="url">The URL to read. Its scheme must be <c>file</c>.</param>
    /// <param name="pathAsIs">
    /// <see langword="true" /> to keep <c>.</c> and <c>..</c> segments, as curl's
    /// <c>--path-as-is</c> does; backslashes become <c>/</c> either way.
    /// </param>
    /// <param name="path">
    /// On success, the parsed pair; otherwise undefined and not to be read.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="url" /> is a <c>file://</c> URL whose
    /// path curl would accept; <see langword="false" /> when the caller should report
    /// <see cref="Abstractions.CurlExitCode.UrlMalformat" />.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The nine steps, in order:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <strong>Scheme.</strong> Compare the scheme to <c>file</c> ignoring case with an
    /// ordinal comparison. Anything else returns <see langword="false" />; this type
    /// never guesses a scheme.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Take the original text.</strong> Work on the remainder of
    /// <see cref="Uri.OriginalString" /> after the leading <c>file:</c>, verbatim, up to
    /// the first <c>?</c> or <c>#</c>, both of which end the path.
    /// <see cref="Uri.AbsolutePath" /> and <see cref="Uri.LocalPath" /> are both
    /// off-limits, for the reasons in the remarks on <see cref="FileUrlPath" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Backslashes.</strong> Turn every <c>\</c> into <c>/</c>, before the
    /// authority is read and whatever <c>pathAsIs</c> says. Measured against curl
    /// 8.21.0: <c>file:///C:/dir\..\secret.txt</c> opens <c>C:/secret.txt</c>,
    /// <c>file://localhost\C:/dir/../nosuch.txt</c> quotes <c>C:/nosuch.txt</c>, and
    /// <c>--path-as-is</c> on <c>file:///C:/dir\..\x</c> quotes <c>C:/dir/../x</c>. An
    /// escaped backslash, <c>%5C</c>, is not converted: it stays an escape.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Authority.</strong> When the remainder starts with <c>//</c>, the
    /// characters up to the next <c>/</c> are the authority. Exactly three authorities
    /// are accepted, and each is dropped so only the path that follows survives: the
    /// empty one (<c>file:///tmp/x</c>), <c>localhost</c> compared ignoring case
    /// (<c>file://localhost/tmp/x</c>) and the literal <c>127.0.0.1</c>
    /// (<c>file://127.0.0.1/tmp/x</c>). Any other authority returns
    /// <see langword="false" />, including <c>[::1]</c>: curl 8.21.0 accepts those three
    /// spellings of "this machine" and no other host part in a <c>file://</c> URL.
    /// One authority is neither accepted-and-dropped nor rejected: exactly two
    /// characters, a single ASCII letter followed by <c>:</c> or <c>|</c>, is not a host
    /// at all but the start of the path, and so it is kept —
    /// <c>file://C:/dir/x</c> and <c>file:///C:/dir/x</c> yield the same pair. Measured
    /// against curl 8.21.0: <c>file://C:/…/ten.txt</c> transfers and exits 0, while
    /// <c>file://D|/nope.txt</c> exits 37 quoting the path <c>D|/nope.txt</c>, which is
    /// only possible if the authority became the head of the path; the letter's case does
    /// not matter, as <c>file://d:/nope.txt</c> also exits 37. The exception is that
    /// narrow: <c>ab:</c>, <c>c</c>, <c>zz</c> and <c>1</c> were each measured at exit 3.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>The UNC form.</strong> <c>file:////server/share</c> leaves
    /// <c>//server/share</c> once its empty authority is dropped. Both leading slashes
    /// are kept: this is the one UNC spelling curl accepts, and collapsing them would
    /// turn a network path into a local one. The server name is not a root that the
    /// next step protects, though: curl 8.21.0 quotes <c>file:////server/../x</c> as
    /// <c>//x</c> and <c>file:////server/share/../../../x</c> as the local <c>/x</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Dot segments.</strong> Unless <c>pathAsIs</c> is <see langword="true" />,
    /// remove every <c>.</c> segment, and every <c>..</c> segment together with the kept
    /// segment before it. A <c>..</c> with nothing before it to remove is dropped, and a
    /// dot segment that ends the path leaves it ending in <c>/</c>, so
    /// <c>C:/dir/..</c> becomes <c>C:/</c>. For a path that begins with <c>/</c> this is
    /// RFC 3986 section 5.2.4. A drive specification (<c>C:</c> or <c>C|</c>, with or
    /// without a leading <c>/</c>) followed by <c>/</c> or by nothing is the root and is
    /// never removed: curl 8.21.0 quotes <c>file:///C:/../../nosuch.txt</c> as
    /// <c>C:/nosuch.txt</c>. Followed by anything else it is an ordinary segment, and
    /// <c>file:///Q:dir/../x</c> is quoted as <c>/x</c>. A dot may be spelled <c>%2e</c>
    /// in either case — curl reads <c>.%2e</c> and <c>%2E%2E</c> as <c>..</c> — but
    /// <c>...</c> is an ordinary segment.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Drive letters.</strong> A path of the form <c>/X:/…</c> or <c>/X|/…</c>,
    /// where <c>X</c> is a single ASCII letter, loses its leading slash so that
    /// <c>file:///c:/Windows/win.ini</c> becomes an absolute Windows path. The <c>|</c>
    /// spelling is preserved as written and not translated to <c>:</c>, because
    /// translating it is a <see cref="Uri.LocalPath" /> behaviour that curl does not
    /// share (ADR-0003).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Decode.</strong> <c>UrlPath</c> is the result of steps two to seven,
    /// still encoded, with each unescaped non-ASCII character encoded as its UTF-8 bytes,
    /// the hexadecimal digits of each well-formed escape uppercased and a malformed one
    /// left as written. <c>OsPath</c> is that text with each <c>%XX</c> escape decoded to
    /// the byte it names and runs of escapes then read as UTF-8, including <c>%2F</c> to
    /// a literal <c>/</c>; a malformed escape — <c>%2</c>, <c>%GG</c>, a trailing
    /// <c>%</c> — is left exactly as written rather than rejected, which is what curl
    /// passes to the operating system.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Hand over.</strong> Convert <c>/</c> to the platform directory separator
    /// in <c>OsPath</c>, and stop there: no trailing separator is trimmed and no case is
    /// changed. A path that is empty once the authority and drive slash are gone returns
    /// <see langword="false" />.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url" /> is <see langword="null" />.
    /// </exception>
    public static bool TryParse(
        Uri url,
        bool pathAsIs,
        [MaybeNullWhen(false)] out FileUrlPath path)
    {
        ArgumentNullException.ThrowIfNull(url);

        path = null;

        if (!string.Equals(url.Scheme, FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = TrimQueryAndFragment(AfterScheme(url.OriginalString))
            .Replace('\\', '/');

        if (!TryDropAuthority(remainder, out string urlPath))
        {
            return false;
        }

        if (!pathAsIs)
        {
            urlPath = RemoveDotSegmentsBelowTheDrive(urlPath);
        }

        urlPath = StripDriveLetterSlash(urlPath);

        if (urlPath.Length == 0)
        {
            return false;
        }

        path = new FileUrlPath(
            UppercaseEscapes(EncodeNonAscii(urlPath)),
            ToOperatingSystemPath(urlPath));

        return true;
    }

    /// <summary>
    /// Takes everything after the first <c>:</c>, which is where the scheme ends.
    /// </summary>
    /// <param name="originalString">The URL exactly as the caller wrote it.</param>
    /// <returns>The scheme-specific part, verbatim.</returns>
    private static string AfterScheme(string originalString)
    {
        int colon = originalString.IndexOf(':');

        return colon < 0 ? string.Empty : originalString[(colon + 1)..];
    }

    /// <summary>
    /// Cuts the text at the first <c>?</c> or <c>#</c>. curl carries neither a query nor
    /// a fragment into a local path.
    /// </summary>
    /// <param name="text">The scheme-specific part of the URL.</param>
    /// <returns>The text up to, but not including, the first of those two characters.</returns>
    private static string TrimQueryAndFragment(string text)
    {
        int cut = text.IndexOfAny(QueryOrFragment);

        return cut < 0 ? text : text[..cut];
    }

    /// <summary>
    /// Removes a leading authority, rejecting any host curl does not accept.
    /// </summary>
    /// <param name="remainder">The scheme-specific part, without query or fragment.</param>
    /// <param name="urlPath">
    /// On success, the still-encoded path with the authority removed.
    /// </param>
    /// <returns>
    /// <see langword="false" /> when an authority is present and is not one curl accepts.
    /// </returns>
    private static bool TryDropAuthority(string remainder, out string urlPath)
    {
        urlPath = remainder;

        if (!remainder.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        int slash = remainder.IndexOf('/', 2);
        string authority = slash < 0 ? remainder[2..] : remainder[2..slash];

        if (IsDriveSpecification(authority))
        {
            urlPath = remainder[2..];

            return true;
        }

        if (!IsAcceptedHost(authority))
        {
            return false;
        }

        urlPath = slash < 0 ? string.Empty : remainder[slash..];

        return true;
    }

    /// <summary>
    /// Reports whether an authority is really the start of a Windows path — <c>C:</c> or
    /// <c>D|</c> — rather than a host.
    /// </summary>
    /// <param name="authority">The authority as written, without its leading <c>//</c>.</param>
    /// <returns>
    /// <see langword="true" /> when the authority is to be kept as the head of the path
    /// instead of being dropped or rejected.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The test is exactly two characters wide because that is what curl 8.21.0 does.
    /// A lone letter with no <c>:</c> or <c>|</c> after it is a rejected host, and that
    /// case does reach here.
    /// </para>
    /// <para>
    /// A longer authority ending in <c>:</c> does not. <c>file://ab:/x</c> is exit 3
    /// upstream, but <see cref="Uri" /> throws <see cref="UriFormatException" /> on it
    /// before this method is called, as it does for <c>file://c:x/y</c>,
    /// <c>file://D|x/y</c> and <c>file://C|D|/x</c>. So no test can cover the width
    /// check against those spellings, and widening it to two-or-more would leave the
    /// suite green. The check is cheap insurance against a future URL type that does
    /// admit them; do not simplify it on the strength of the tests passing.
    /// </para>
    /// </remarks>
    private static bool IsDriveSpecification(string authority) =>
        authority.Length == 2
        && char.IsAsciiLetter(authority[0])
        && (authority[1] == ':' || authority[1] == '|');

    /// <summary>
    /// Reports whether an authority is one of the three curl 8.21.0 accepts for
    /// <c>file://</c>.
    /// </summary>
    /// <param name="authority">The authority as written, without its leading <c>//</c>.</param>
    /// <returns><see langword="true" /> when the authority may be dropped.</returns>
    /// <remarks>
    /// A drive specification never reaches here: <see cref="IsDriveSpecification" /> has
    /// already claimed it as path text, because curl reads <c>file://C:/x</c> as a path
    /// and not as a host named <c>C:</c>.
    /// </remarks>
    private static bool IsAcceptedHost(string authority) =>
        authority.Length == 0
        || string.Equals(authority, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(authority, "127.0.0.1", StringComparison.Ordinal);

    /// <summary>
    /// Drops the slash in front of a <c>/X:/…</c> or <c>/X|/…</c> drive specification.
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
    /// Removes the dot segments from a path, keeping a leading drive specification as
    /// the root they cannot climb above.
    /// </summary>
    /// <param name="urlPath">The still-encoded path, its authority already dropped.</param>
    /// <returns>The path with no <c>.</c> or <c>..</c> segment left.</returns>
    private static string RemoveDotSegmentsBelowTheDrive(string urlPath)
    {
        int rootLength = DriveRootLength(urlPath);

        return urlPath[..rootLength] + RemoveDotSegments(urlPath[rootLength..]);
    }

    /// <summary>
    /// Measures the drive specification at the head of a path, with its optional leading
    /// <c>/</c>, when it is a root: followed by <c>/</c> or by nothing at all.
    /// </summary>
    /// <param name="urlPath">The still-encoded path, its authority already dropped.</param>
    /// <returns>
    /// The length of the root — <c>/C:</c> is three, <c>C:</c> is two — or zero when the
    /// path does not begin with a drive specification that is a root.
    /// </returns>
    private static int DriveRootLength(string urlPath)
    {
        int end = (urlPath.StartsWith('/') ? 1 : 0) + 2;

        bool isRoot = urlPath.Length >= end
            && IsDriveSpecification(urlPath[(end - 2)..end])
            && (urlPath.Length == end || urlPath[end] == '/');

        return isRoot ? end : 0;
    }

    /// <summary>
    /// Removes <c>.</c> segments, and <c>..</c> segments with the kept segment before each.
    /// </summary>
    /// <param name="path">The still-encoded path below any drive specification.</param>
    /// <returns>
    /// The path without dot segments, keeping its leading <c>/</c> if it had one and
    /// ending in <c>/</c> when its last segment was a dot segment.
    /// </returns>
    private static string RemoveDotSegments(string path)
    {
        bool rooted = path.StartsWith('/');
        string[] segments = (rooted ? path[1..] : path).Split('/');
        var kept = new List<string>(segments.Length);

        foreach (string segment in segments)
        {
            KeepOrDropSegment(kept, segment);
        }

        if (IsDotSegment(segments[^1]))
        {
            kept.Add(string.Empty);
        }

        return (rooted ? "/" : string.Empty) + string.Join('/', kept);
    }

    /// <summary>
    /// Applies one segment to the segments kept so far: a <c>..</c> removes the last kept
    /// segment, a <c>.</c> is dropped, and anything else is kept.
    /// </summary>
    /// <param name="kept">The segments kept so far.</param>
    /// <param name="segment">One still-encoded path segment.</param>
    private static void KeepOrDropSegment(List<string> kept, string segment)
    {
        string dots = SpellDotsPlainly(segment);

        if (dots == "..")
        {
            RemoveLastKept(kept);
        }
        else if (dots != ".")
        {
            kept.Add(segment);
        }
    }

    /// <summary>
    /// Tells whether a segment is <c>.</c> or <c>..</c>, its dots plain or encoded.
    /// </summary>
    /// <param name="segment">One still-encoded path segment.</param>
    /// <returns><see langword="true" /> for a dot segment.</returns>
    private static bool IsDotSegment(string segment) =>
        SpellDotsPlainly(segment) is "." or "..";

    /// <summary>
    /// Rewrites each <c>%2e</c> escape, in either case, as the <c>.</c> it encodes, so
    /// that a segment can be compared with <c>.</c> and <c>..</c>.
    /// </summary>
    /// <param name="segment">One still-encoded path segment.</param>
    /// <returns>The segment with its encoded dots decoded and nothing else changed.</returns>
    private static string SpellDotsPlainly(string segment) =>
        segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Removes the last kept segment, if there is one.
    /// </summary>
    /// <param name="kept">The segments kept so far.</param>
    private static void RemoveLastKept(List<string> kept)
    {
        if (kept.Count > 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }
    }

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
