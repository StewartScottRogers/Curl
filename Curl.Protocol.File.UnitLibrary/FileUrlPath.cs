using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Protocol.File;

/// <summary>
/// The path half of a <c>file://</c> URL, in both of the forms curl needs it: as written
/// in the URL, and as handed to the operating system.
/// </summary>
/// <param name="UrlPath">
/// The path exactly as it appears in the URL, still percent-encoded. This is the text
/// curl echoes in its exit 37 message, so it has to survive parsing unaltered rather than
/// be reconstructed from the decoded form.
/// </param>
/// <param name="OsPath">
/// The percent-decoded operating-system path handed to
/// <see cref="Abstractions.IFileSystem" />. Directory separators are the platform's, and
/// no <c>.</c> or <c>..</c> segment has been resolved.
/// </param>
/// <remarks>
/// <para>
/// Parsing works from <see cref="Uri.OriginalString" />, never
/// <see cref="Uri.AbsolutePath" /> and never <see cref="Uri.LocalPath" />. Those two
/// normalise <c>..</c> away, rewrite <c>c|</c> to <c>c:</c>, and fold
/// <c>file:////server/share</c> into a UNC authority — none of which curl does. The
/// measurements behind that are recorded in ADR-0003
/// (<c>Documentation/Planning/Decisions</c>), taken against curl 8.21.0.
/// </para>
/// <para>
/// The wider question of URLs <see cref="Uri" /> cannot round-trip at all — a
/// <c>%2F</c> in the drive position, a userinfo component — is task BL-010; those URLs
/// never reach this type, because <see cref="Uri" /> throws before it is called.
/// </para>
/// </remarks>
public sealed record FileUrlPath(string UrlPath, string OsPath)
{
    /// <summary>
    /// The only scheme this type parses, compared ordinally and ignoring case.
    /// </summary>
    private const string FileScheme = "file";

    /// <summary>
    /// The two characters that end the path and begin something this type discards.
    /// </summary>
    private static readonly char[] QueryOrFragment = ['?', '#'];

    /// <summary>
    /// Extracts the URL path and the operating-system path from a <c>file://</c> URL.
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
    /// <para>
    /// The seven steps, in order:
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
    /// turn a network path into a local one.
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
    /// <strong>Decode.</strong> <c>UrlPath</c> is the result of steps three to five,
    /// still encoded. <c>OsPath</c> is that text with each <c>%XX</c> escape decoded to
    /// the byte it names and runs of escapes then read as UTF-8, including <c>%2F</c> to
    /// a literal <c>/</c>; a malformed escape — <c>%2</c>, <c>%GG</c>, a trailing
    /// <c>%</c> — is left exactly as written rather than rejected, which is what curl
    /// passes to the operating system.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Hand over unnormalised.</strong> Convert <c>/</c> to the platform
    /// directory separator in <c>OsPath</c>, and stop there: no <c>.</c> or <c>..</c>
    /// segment is resolved, no trailing separator is trimmed and no case is changed. curl
    /// gives the path to the operating system as the user wrote it. An empty path after
    /// all of this returns <see langword="false" />.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url" /> is <see langword="null" />.
    /// </exception>
    public static bool TryParse(Uri url, [MaybeNullWhen(false)] out FileUrlPath path)
    {
        ArgumentNullException.ThrowIfNull(url);

        path = null;

        if (!string.Equals(url.Scheme, FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = TrimQueryAndFragment(AfterScheme(url.OriginalString));

        if (!TryDropAuthority(remainder, out string urlPath))
        {
            return false;
        }

        urlPath = StripDriveLetterSlash(urlPath);

        if (urlPath.Length == 0)
        {
            return false;
        }

        path = new FileUrlPath(urlPath, ToOperatingSystemPath(urlPath));

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
    /// Decodes the escapes and switches to the platform's directory separator, resolving
    /// nothing else.
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
