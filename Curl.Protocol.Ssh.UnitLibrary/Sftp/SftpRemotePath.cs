using System.Globalization;
using System.Text;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Turns an <c>sftp://</c> URL's path into the path curl 8.21.0 sends: percent-decoded to
/// raw bytes, with a leading <c>/~/</c> replaced by the home directory the server names
/// for <c>.</c> and a slash. Measured 2026-09-29 (BL-569): <c>/a%20b.txt</c> opens
/// <c>/a b.txt</c>, <c>/x%2Fy</c> opens <c>/x/y</c>, and <c>/~/dir/f</c> opens
/// <c>&lt;home&gt;/dir/f</c>.
/// </summary>
internal static class SftpRemotePath
{
    private static readonly byte[] HomePrefix = "/~/"u8.ToArray();

    /// <summary>
    /// Decodes <paramref name="urlPath" />: each <c>%XX</c> becomes that byte, and every
    /// other character its UTF-8 bytes.
    /// </summary>
    /// <param name="urlPath">The URL's path, as parsed, with its percent-escapes.</param>
    /// <returns>The path's bytes.</returns>
    internal static byte[] Decode(string urlPath)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(urlPath);
        List<byte> bytes = new(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (IsEscape(encoded, index))
            {
                bytes.Add(byte.Parse(Encoding.ASCII.GetString(encoded, index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 2;
            }
            else
            {
                bytes.Add(encoded[index]);
            }
        }

        return [.. bytes];
    }

    /// <summary>
    /// Resolves a decoded path against the home directory: <c>/~/rest</c> becomes
    /// <c>&lt;home&gt;/rest</c>, and any other path is sent as it is.
    /// </summary>
    /// <param name="path">The decoded path.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>.</param>
    /// <returns>The path to send.</returns>
    internal static byte[] Resolve(byte[] path, byte[] homeDirectory) =>
        path.AsSpan().StartsWith(HomePrefix) ? [.. homeDirectory, (byte)'/', .. path.AsSpan(HomePrefix.Length)] : path;

    // A percent sign followed by two hex digits; '%' and hex digits are single UTF-8 bytes.
    private static bool IsEscape(byte[] encoded, int index) =>
        encoded[index] == '%' && index + 2 < encoded.Length && char.IsAsciiHexDigit((char)encoded[index + 1]) && char.IsAsciiHexDigit((char)encoded[index + 2]);
}
