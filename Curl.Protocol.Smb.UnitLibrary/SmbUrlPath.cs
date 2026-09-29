using System.Globalization;
using System.Text;

namespace Curl.Protocol.Smb;

/// <summary>
/// The share and file an <c>smb</c> URL's path names, split as curl 8.21.0's
/// <c>smb_parse_url_path</c> splits it: percent-decoded, one leading <c>/</c> or <c>\</c>
/// dropped, the share up to the next <c>/</c> (or <c>\</c> when there is none), and the
/// file after it with every <c>/</c> turned into <c>\</c>.
/// </summary>
/// <param name="Share">The share's name, as decoded bytes.</param>
/// <param name="FilePath">The file's path within the share, as decoded bytes, <c>\</c>-separated.</param>
internal sealed record SmbUrlPath(byte[] Share, byte[] FilePath)
{
    private const byte FirstPrintable = 0x20;

    /// <summary>
    /// Parses the URL's path, or gives curl's exit 3 message: <see cref="SmbMessages.UrlMalformat" />
    /// for a percent-encoded control character, <see cref="SmbMessages.MissingShare" /> when
    /// no separator follows the share.
    /// </summary>
    /// <param name="absolutePath">The URL's path, still percent-encoded.</param>
    /// <param name="path">The share and file, when the path is accepted.</param>
    /// <returns><see langword="null" /> when the path is accepted, else the error message.</returns>
    public static string? TryParse(string absolutePath, out SmbUrlPath? path)
    {
        path = null;
        byte[]? decoded = Decode(absolutePath);
        if (decoded is null)
        {
            return SmbMessages.UrlMalformat;
        }

        path = Split(decoded.AsSpan(StartsWithSeparator(decoded) ? 1 : 0));
        return path is null ? SmbMessages.MissingShare : null;
    }

    private static bool StartsWithSeparator(byte[] decoded) =>
        decoded.Length > 0 && (decoded[0] == '/' || decoded[0] == '\\');

    private static SmbUrlPath? Split(ReadOnlySpan<byte> rest)
    {
        int separator = rest.IndexOf((byte)'/');
        if (separator < 0)
        {
            separator = rest.IndexOf((byte)'\\');
        }

        if (separator < 0)
        {
            return null;
        }

        byte[] filePath = rest[(separator + 1)..].ToArray();
        filePath.AsSpan().Replace((byte)'/', (byte)'\\');
        return new SmbUrlPath(rest[..separator].ToArray(), filePath);
    }

    // Curl_urldecode with REJECT_CTRL: "%XX" with two hex digits becomes that byte, any
    // other character stays as its UTF-8 bytes, and a byte below 0x20 refuses the path.
    private static byte[]? Decode(string encoded)
    {
        byte[] text = Encoding.UTF8.GetBytes(encoded);
        var decoded = new List<byte>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            byte value = text[index];
            if (value == '%' && TryReadHexByte(text.AsSpan(index + 1), out byte escaped))
            {
                value = escaped;
                index += 2;
            }

            if (value < FirstPrintable)
            {
                return null;
            }

            decoded.Add(value);
        }

        return [.. decoded];
    }

    private static bool TryReadHexByte(ReadOnlySpan<byte> text, out byte value)
    {
        value = 0;
        return text.Length >= 2
            && byte.TryParse(Encoding.ASCII.GetString(text[..2]), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }
}
