using System.Text;

namespace Curl.Protocol.Ftp;

/// <summary>
/// An <c>ftp://</c> URL's path split as curl 8.21.0 walks it by default: one <c>CWD</c>
/// per directory, then the file name.
/// </summary>
/// <param name="Directories">
/// Each directory to change into, in order, percent-decoded; empty segments, as in
/// <c>a//b</c>, are skipped.
/// </param>
/// <param name="FileName">
/// The last segment, percent-decoded; empty when the path ends in <c>/</c>, which asks for
/// a directory listing.
/// </param>
/// <remarks>
/// Decoded bytes are held one per <see cref="char" /> (Latin-1), so they reach the server
/// exactly as decoded: <c>caf%C3%A9</c> is sent as the bytes <c>63 61 66 C3 A9</c>.
/// </remarks>
internal sealed record FtpUrlPath(IReadOnlyList<string> Directories, string FileName)
{
    /// <summary>
    /// Splits and decodes <paramref name="absolutePath" />.
    /// </summary>
    /// <param name="absolutePath">The URL's path, still percent-encoded, starting with <c>/</c>.</param>
    /// <returns>
    /// The split path, or <see langword="null" /> when a segment decodes to a byte below
    /// 0x20, which curl refuses with exit 3 and <c>path contains control characters</c>.
    /// </returns>
    public static FtpUrlPath? Parse(string absolutePath)
    {
        string[] segments = absolutePath.Split('/');
        var decoded = new string[segments.Length];
        for (int index = 0; index < segments.Length; index++)
        {
            decoded[index] = PercentDecode(segments[index]);
            if (decoded[index].Any(character => character < ' '))
            {
                return null;
            }
        }

        string[] directories = [.. decoded[..^1].Where(segment => segment.Length > 0)];
        return new FtpUrlPath(directories, decoded[^1]);
    }

    private static string PercentDecode(string segment)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(segment);
        var bytes = new List<byte>(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'%' && TryDecodeEscape(encoded, index, out byte value))
            {
                bytes.Add(value);
                index += 2;
            }
            else
            {
                bytes.Add(encoded[index]);
            }
        }

        return Encoding.Latin1.GetString([.. bytes]);
    }

    private static bool TryDecodeEscape(byte[] encoded, int percentIndex, out byte value)
    {
        value = 0;
        return percentIndex + 2 < encoded.Length
            && byte.TryParse(
                Encoding.ASCII.GetString(encoded, percentIndex + 1, 2),
                System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
    }
}
