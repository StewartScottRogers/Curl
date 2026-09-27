namespace Curl.Console;

/// <summary>
/// The file name <c>-O</c> / <c>--remote-name</c> and <c>--remote-name-all</c> take from a
/// URL's path, as curl 8.21.0 takes it: the last segment that is not empty, still
/// percent-encoded, without the query or fragment.
/// </summary>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-239 Notes), <c>curl -sS -O &lt;url&gt;</c>
/// against a loopback server: <c>/dir/file.txt?q=1#f</c> writes <c>file.txt</c>,
/// <c>/dir/</c> writes <c>dir</c>, <c>/a/b//</c> writes <c>b</c>, <c>/a%20b%3F.txt</c> writes
/// <c>a%20b%3F.txt</c>, <c>file:///C:/Windows/win.ini</c> writes <c>win.ini</c>, and <c>/</c>
/// or no path writes <see cref="Fallback" /> after <see cref="NoRemoteNameWarning" />.
/// </remarks>
internal static class RemoteFileName
{
    /// <summary>The name curl 8.21.0 uses when the URL's path names no file.</summary>
    internal const string Fallback = "curl_response";

    /// <summary>
    /// The warning curl 8.21.0 prints, unless <c>-s</c> was given, before a transfer whose URL
    /// path names no file.
    /// </summary>
    internal const string NoRemoteNameWarning = "Warning: No remote filename, uses \"" + Fallback + "\"";

    /// <summary>
    /// Takes the file name from a URL's path.
    /// </summary>
    /// <param name="absolutePath">The URL's path, as <see cref="Protocol.Abstractions.CurlUrl.AbsolutePath" /> gives it.</param>
    /// <returns>
    /// The last segment of the path that is not empty, or <see langword="null" /> when every
    /// segment is empty.
    /// </returns>
    internal static string? FromUrlPath(string absolutePath)
    {
        string trimmed = absolutePath.TrimEnd('/');
        string name = trimmed[(trimmed.LastIndexOf('/') + 1)..];

        return name.Length == 0 ? null : name;
    }
}
