using Curl.Protocol.Ssh.Sftp;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Turns an <c>scp://</c> URL's path into the path curl 8.21.0 asks <c>scp</c> for:
/// percent-decoded to raw bytes, with a leading <c>/~/</c> dropped so the path is relative
/// to the home directory. Measured 2026-09-29 (BL-574): <c>/~/f</c> and <c>/%7E/f</c> ask
/// for <c>f</c>, <c>/a%20b.txt</c> for <c>/a b.txt</c>, and <c>/~/</c> and <c>/~</c> are
/// sent as they are.
/// </summary>
internal static class ScpRemotePath
{
    private static readonly byte[] HomePrefix = "/~/"u8.ToArray();

    /// <summary>
    /// Resolves <paramref name="urlPath" /> as curl's <c>Curl_getworkingpath</c> does for
    /// SCP.
    /// </summary>
    /// <param name="urlPath">The URL's path, as parsed, with its percent-escapes.</param>
    /// <returns>The path's bytes.</returns>
    internal static byte[] Resolve(string urlPath)
    {
        byte[] path = SftpRemotePath.Decode(urlPath);
        return path.Length > HomePrefix.Length && path.AsSpan().StartsWith(HomePrefix) ? path[HomePrefix.Length..] : path;
    }
}
