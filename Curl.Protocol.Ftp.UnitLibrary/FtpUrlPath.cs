using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// An <c>ftp://</c> URL's path split as curl 8.21.0 walks it under <c>--ftp-method</c>:
/// the <c>CWD</c> arguments, the name the file commands carry, and the argument a listing
/// carries.
/// </summary>
/// <param name="Directories">
/// Each <c>CWD</c> argument, in order, percent-decoded: for <c>multicwd</c> <c>/</c> when
/// the decoded path starts with <c>/</c>, then one per directory (empty segments, as in
/// <c>a//b</c>, skipped), the whole directory part
/// for <c>singlecwd</c>, none for <c>nocwd</c>.
/// </param>
/// <param name="FileName">
/// The name <c>SIZE</c>, <c>RETR</c>, <c>STOR</c> and the rest carry, percent-decoded: the
/// last segment, or for <c>nocwd</c> the whole path without its leading <c>/</c>. Empty
/// when the path ends in <c>/</c>, which asks for a directory listing.
/// </param>
/// <param name="ListArgument">
/// The argument <c>LIST</c> or <c>NLST</c> carries: for <c>nocwd</c> the directory part,
/// <see langword="null" /> otherwise and when the path has no directory part.
/// </param>
/// <remarks>
/// <para>
/// Decoded bytes are held one per <see cref="char" /> (Latin-1), so they reach the server
/// exactly as decoded: <c>caf%C3%A9</c> is sent as the bytes <c>63 61 66 C3 A9</c>.
/// </para>
/// <para>
/// Every method decodes the whole path before splitting it, as curl does, so <c>%2F</c>
/// splits a segment. <c>singlecwd</c> and <c>nocwd</c> split it at its last <c>/</c>; a
/// directory part that is empty because the path starts with <c>//</c> is <c>/</c>
/// (BL-436). <c>multicwd</c> splits it at every <c>/</c> (BL-446).
/// </para>
/// </remarks>
internal sealed record FtpUrlPath(IReadOnlyList<string> Directories, string FileName, string? ListArgument = null)
{
    /// <summary>
    /// Splits and decodes <paramref name="absolutePath" /> for <paramref name="method" />.
    /// </summary>
    /// <param name="absolutePath">The URL's path, still percent-encoded, starting with <c>/</c>.</param>
    /// <param name="method">How <c>--ftp-method</c> reaches the file.</param>
    /// <returns>
    /// The split path, or <see langword="null" /> when the path decodes to a byte below
    /// 0x20, which curl refuses with exit 3 and <c>path contains control characters</c>.
    /// </returns>
    public static FtpUrlPath? Parse(string absolutePath, FtpFileMethod method)
    {
        string[] segments = absolutePath.Split('/');
        string[] decoded = [.. segments.Select(PercentDecode)];
        if (decoded.Any(segment => segment.Any(character => character < ' ')))
        {
            return null;
        }

        return method switch
        {
            FtpFileMethod.NoCwd => SplitForNoCwd(string.Join('/', decoded[1..])),
            FtpFileMethod.SingleCwd => SplitForSingleCwd(string.Join('/', decoded[1..])),
            _ => SplitForMultiCwd(string.Join('/', decoded[1..])),
        };
    }

    private static FtpUrlPath SplitForMultiCwd(string path)
    {
        string[] segments = path.Split('/');
        IEnumerable<string> directories = segments[..^1].Where(segment => segment.Length > 0);
        return new FtpUrlPath([.. path.StartsWith('/') ? directories.Prepend("/") : directories], segments[^1]);
    }

    private static FtpUrlPath SplitForSingleCwd(string path)
    {
        int slash = path.LastIndexOf('/');
        if (slash < 0)
        {
            return new FtpUrlPath([], path);
        }

        string directory = slash == 0 ? "/" : path[..slash];
        return new FtpUrlPath([directory], path[(slash + 1)..]);
    }

    private static FtpUrlPath SplitForNoCwd(string path)
    {
        int slash = path.LastIndexOf('/');
        string fileName = path[(slash + 1)..];
        string? listArgument = slash < 0 ? null : path[..Math.Max(slash, 1)];
        return new FtpUrlPath([], fileName.Length == 0 ? string.Empty : path, listArgument);
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
