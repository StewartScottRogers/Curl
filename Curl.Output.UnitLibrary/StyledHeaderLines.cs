using System.Buffers;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Styles the header lines <c>-i</c> and <c>-I</c> write to a terminal under
/// <c>--styled-output</c>, as curl 8.21.0's <c>tool_header_cb</c> in <c>src/tool_cb_hdr.c</c>
/// does: the name before a line's first colon in bold, and, off Windows, a <c>Location</c>
/// value wrapped in an OSC 8 hyperlink to the URL it resolves to (ADR-0246, BL-736).
/// </summary>
/// <remarks>
/// <para>
/// A line with no colon, such as the status line or the blank line ending the head, is
/// written as it is. Bold is <c>ESC [1m</c>; it is switched off with <c>ESC [22m</c> on
/// Windows and with <c>ESC [0m</c> elsewhere, where curl links <c>Location</c> too. Measured
/// with curl 8.18.0 on Linux under <c>script</c> (BL-736 Notes); the Windows bytes are
/// curl-8_21_0's source.
/// </para>
/// <para>
/// curl links a line whose name is a case-insensitive prefix of <c>Location</c>, empty
/// included, as its <c>curl_strnequal("Location", name, namelen)</c> does; the link is the
/// value resolved against the URL of the transfer, and only an <c>http</c>, <c>https</c>,
/// <c>ftp</c> or <c>ftps</c> one is linked. Each linked value becomes the URL the next one
/// resolves against, as curl's effective URL does when <c>-L</c> follows it. curl links
/// nothing inside VTE 0.48.1 or older (<c>VTE_VERSION</c> at most 4801).
/// </para>
/// </remarks>
public sealed class StyledHeaderLines
{
    /// <summary>The newest <c>VTE_VERSION</c> curl writes no hyperlink for.</summary>
    private const long NewestVteWithoutHyperlinks = 4801;

    private static readonly byte[] BoldOn = "\e[1m"u8.ToArray();

    private static readonly byte[] WindowsBoldOff = "\e[22m"u8.ToArray();

    private static readonly byte[] UnixBoldOff = "\e[0m"u8.ToArray();

    private static readonly byte[] HyperlinkStart = "\e]8;;"u8.ToArray();

    private static readonly byte[] StringTerminator = "\e\\"u8.ToArray();

    private static readonly string[] LinkedSchemes = ["http", "https", "ftp", "ftps"];

    private readonly byte[] boldOff;

    private readonly bool linksLocation;

    private string baseUrl;

    private StyledHeaderLines(byte[] boldOff, bool linksLocation, string baseUrl)
    {
        this.boldOff = boldOff;
        this.linksLocation = linksLocation;
        this.baseUrl = baseUrl;
    }

    /// <summary>
    /// Creates the styles curl uses on this platform.
    /// </summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows, where curl links nothing.</param>
    /// <param name="transferUrl">The absolute URL of the transfer, which a relative <c>Location</c> resolves against.</param>
    /// <param name="vteVersion">The <c>VTE_VERSION</c> environment variable, or <see langword="null" /> when it is not set.</param>
    /// <returns>The styles.</returns>
    public static StyledHeaderLines ForPlatform(bool runsOnWindows, string transferUrl, string? vteVersion)
    {
        ArgumentNullException.ThrowIfNull(transferUrl);

        return runsOnWindows
            ? new(WindowsBoldOff, linksLocation: false, transferUrl)
            : new(UnixBoldOff, !IsVteWithoutHyperlinks(vteVersion), transferUrl);
    }

    /// <summary>
    /// Styles one header line.
    /// </summary>
    /// <param name="line">The line, its line end included.</param>
    /// <returns>The bytes to write in its place.</returns>
    public byte[] Style(ReadOnlySpan<byte> line)
    {
        int colon = line.IndexOf((byte)':');
        if (colon < 0)
        {
            return line.ToArray();
        }

        ArrayBufferWriter<byte> styled = new(line.Length + 32);
        ReadOnlySpan<byte> name = line[..colon];
        styled.Write(BoldOn);
        styled.Write(name);
        styled.Write(boldOff);
        styled.Write(":"u8);
        ReadOnlySpan<byte> value = line[(colon + 1)..];
        if (linksLocation && NamesLocation(name))
        {
            WriteLinkedLocation(styled, value);
        }
        else
        {
            styled.Write(value);
        }

        return styled.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Tells whether <paramref name="vteVersion" /> names VTE 0.48.1 or older, as curl's
    /// <c>curlx_str_number</c> reads it: the leading decimal digits, none meaning no version.
    /// </summary>
    private static bool IsVteWithoutHyperlinks(string? vteVersion)
    {
        string digits = new([.. (vteVersion ?? string.Empty).TakeWhile(char.IsAsciiDigit)]);
        return long.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long version)
            && version <= NewestVteWithoutHyperlinks;
    }

    /// <summary>Tells whether <paramref name="name" /> is a case-insensitive prefix of <c>Location</c>.</summary>
    private static bool NamesLocation(ReadOnlySpan<byte> name) =>
        name.Length <= "Location".Length && Ascii.EqualsIgnoreCase(name, "Location"u8[..name.Length]);

    /// <summary>
    /// Writes a <c>Location</c> value, after its colon, as curl's <c>write_linked_location</c>
    /// does: its leading blanks, then the rest of it, line end included, inside a hyperlink to
    /// the URL it resolves to; or the value as it is when it resolves to no URL curl links.
    /// </summary>
    private void WriteLinkedLocation(ArrayBufferWriter<byte> styled, ReadOnlySpan<byte> value)
    {
        int blanks = value.Length - value.TrimStart(" \t"u8).Length;
        ReadOnlySpan<byte> location = value[blanks..];
        string? target = LinkTarget(baseUrl, Encoding.Latin1.GetString(location.TrimEnd("\r\n"u8)));
        if (target is null)
        {
            styled.Write(value);
            return;
        }

        styled.Write(value[..blanks]);
        styled.Write(HyperlinkStart);
        styled.Write(Encoding.UTF8.GetBytes(target));
        styled.Write(StringTerminator);
        styled.Write(location);
        styled.Write(HyperlinkStart);
        styled.Write(StringTerminator);
        baseUrl = target;
    }

    /// <summary>
    /// Resolves <paramref name="location" /> against <paramref name="baseUrl" />.
    /// </summary>
    /// <returns>
    /// The absolute URL, or <see langword="null" /> when <paramref name="location" /> is empty or
    /// holds a space or control character (which curl's URL parser rejects), when either does
    /// not parse, or when the result's scheme is not one curl links.
    /// </returns>
    private static string? LinkTarget(string baseUrl, string location)
    {
        if (!IsUrlText(location))
        {
            return null;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
            && Uri.TryCreate(baseUri, location, out Uri? target)
            && LinkedSchemes.Contains(target.Scheme)
                ? target.AbsoluteUri
                : null;
    }

    /// <summary>
    /// Tells whether <paramref name="location" /> could be a URL for curl's URL parser: not empty,
    /// and with no space or control character.
    /// </summary>
    private static bool IsUrlText(string location) =>
        location.Length > 0 && !location.Any(character => character <= ' ' || character == '\x7f');
}
