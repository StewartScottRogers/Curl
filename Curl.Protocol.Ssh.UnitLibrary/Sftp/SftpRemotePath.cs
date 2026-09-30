using System.Globalization;
using System.Text;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Turns an <c>sftp://</c> URL's path into the path curl 8.21.0 sends: percent-decoded to
/// raw bytes, with <c>/~</c> or a leading <c>/~/</c> replaced by the home directory the
/// server names for <c>.</c> and a slash. Measured 2026-09-29 (BL-569): <c>/a%20b.txt</c> opens
/// <c>/a b.txt</c>, <c>/x%2Fy</c> opens <c>/x/y</c>, and <c>/~/dir/f</c> opens
/// <c>&lt;home&gt;/dir/f</c>.
/// </summary>
internal static class SftpRemotePath
{
    private static readonly byte[] HomePrefix = "/~/"u8.ToArray();

    private static readonly byte[] HomeAlone = "/~"u8.ToArray();

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
    /// Decodes <paramref name="urlPath" /> and resolves it against the home directory, as
    /// curl 8.21.0's <c>Curl_getworkingpath</c> does once <c>REALPATH .</c> has answered.
    /// Measured 2026-09-30 (BL-974): a <c>%00</c> in the path ends the transfer with exit 3
    /// after <c>REALPATH</c>, and nothing is opened.
    /// </summary>
    /// <param name="urlPath">The URL's path, as parsed, with its percent-escapes.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>.</param>
    /// <returns>The path to send.</returns>
    /// <exception cref="SshTransferException">
    /// The path holds <c>%00</c> (exit 3, <c>URL using bad/illegal format or missing URL</c>).
    /// </exception>
    internal static byte[] ResolveUrlPath(string urlPath, byte[] homeDirectory)
    {
        byte[] path = Decode(urlPath);
        return Array.IndexOf(path, (byte)0) < 0 ? Resolve(path, homeDirectory) : throw SshTransferException.UrlPathHoldsZeroByte();
    }

    /// <summary>
    /// Resolves a decoded path against the home directory: <c>/~</c> becomes
    /// <c>&lt;home&gt;/</c>, <c>/~/rest</c> becomes <c>&lt;home&gt;/rest</c> with no second
    /// slash when the home directory ends with one (or is empty), and any other path is sent
    /// as it is. Measured 2026-09-30 (BL-974): <c>/~</c> lists
    /// <c>/home/stewart_rogers/</c>.
    /// </summary>
    /// <param name="path">The decoded path.</param>
    /// <param name="homeDirectory">The server's answer to <c>REALPATH .</c>.</param>
    /// <returns>The path to send.</returns>
    internal static byte[] Resolve(byte[] path, byte[] homeDirectory)
    {
        if (path.AsSpan().SequenceEqual(HomeAlone))
        {
            return Concatenate(homeDirectory, "/"u8);
        }

        if (!path.AsSpan().StartsWith(HomePrefix))
        {
            return path;
        }

        bool homeEndsWithSlash = homeDirectory.Length == 0 || homeDirectory[^1] == '/';
        return Concatenate(homeDirectory, path.AsSpan(homeEndsWithSlash ? HomePrefix.Length : HomeAlone.Length));
    }

    /// <summary>
    /// Gets whether <paramref name="urlPath" /> names a directory, which curl lists rather
    /// than downloads: its decoded path ends with a slash, or is <c>/~</c>, which resolves to
    /// the home directory and a slash. Measured 2026-09-29 (BL-570): <c>/d/x%2F</c> lists
    /// <c>/d/x/</c>; 2026-09-30 (BL-974): <c>/~</c> lists the home directory.
    /// </summary>
    /// <param name="urlPath">The URL's path, with its percent-escapes.</param>
    /// <returns><see langword="true" /> when the decoded path ends with <c>/</c> or is <c>/~</c>.</returns>
    internal static bool NamesDirectory(string urlPath)
    {
        ReadOnlySpan<byte> path = Decode(urlPath);
        return path.EndsWith("/"u8) || path.SequenceEqual(HomeAlone);
    }

    private static byte[] Concatenate(byte[] homeDirectory, ReadOnlySpan<byte> rest)
    {
        byte[] joined = new byte[homeDirectory.Length + rest.Length];
        homeDirectory.CopyTo(joined, 0);
        rest.CopyTo(joined.AsSpan(homeDirectory.Length));
        return joined;
    }

    // A percent sign followed by two hex digits; '%' and hex digits are single UTF-8 bytes.
    private static bool IsEscape(byte[] encoded, int index) =>
        encoded[index] == '%' && index + 2 < encoded.Length && char.IsAsciiHexDigit((char)encoded[index + 1]) && char.IsAsciiHexDigit((char)encoded[index + 2]);
}
