namespace Curl.Console;

/// <summary>
/// The file name <c>-O</c> / <c>--remote-name</c> and <c>--remote-name-all</c> take from a
/// URL's path, as curl 8.21.0 takes it: the last segment that is not empty, still
/// percent-encoded, without the query or fragment, after its <c>.</c> and <c>..</c> segments
/// are resolved (which only <c>--path-as-is</c> leaves in the path).
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
    /// <remarks>
    /// A backslash also ends a name, though not a dot segment: <c>/a\b.txt</c> writes
    /// <c>b.txt</c> and <c>/x/a\</c> writes <c>a</c>, on Windows and Linux alike (curl 8.21.0
    /// measured 2026-10-28, CGR-064, BL-3593).
    /// </remarks>
    /// <returns>
    /// The last part that is not empty of the path with its dot segments resolved, split at
    /// <c>/</c> and <c>\</c>, or <see langword="null" /> when no such part is left.
    /// </returns>
    internal static string? FromUrlPath(string absolutePath)
    {
        var segments = new List<string>();
        foreach (string segment in absolutePath.Split('/'))
        {
            KeepOrDropSegment(segments, segment);
        }

        return segments
            .SelectMany(segment => segment.Split('\\'))
            .LastOrDefault(part => part.Length != 0);
    }

    /// <summary>
    /// Adds one path segment to the segments kept so far, as curl 8.21.0's <c>dedotdotify</c>
    /// resolves it before the name is taken: a <c>.</c> segment is dropped and a <c>..</c>
    /// segment drops the one before it, with <c>%2e</c> or <c>%2E</c> counted as a dot, so
    /// <c>--path-as-is</c> on <c>/a/..</c> names no file and <c>/a/.</c> names <c>a</c>
    /// (measured 2026-10-28, BL-3592).
    /// </summary>
    /// <param name="segments">The segments kept so far.</param>
    /// <param name="segment">The next segment, still percent-encoded.</param>
    private static void KeepOrDropSegment(List<string> segments, string segment)
    {
        string dotted = segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);
        if (dotted == "..")
        {
            if (segments.Count != 0)
            {
                segments.RemoveAt(segments.Count - 1);
            }
        }
        else if (dotted != ".")
        {
            segments.Add(segment);
        }
    }
}
