using System.Buffers;

namespace Curl.Cli;

/// <summary>
/// Completes the URL of a <c>-T</c> / <c>--upload-file</c> transfer: when the URL path
/// names no file, the local file's base name is appended, deciding whether and what to
/// append the way curl's <c>add_file_name_to_url</c> does. Pure string work; nothing
/// here reads the file system.
/// </summary>
public static class UploadUrl
{
    private const int MaximumSlashesAfterScheme = 3;

    private static readonly SearchValues<char> SchemeCharacters = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+.-");

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="uploadFile"/> is exactly
    /// <c>-</c> or <c>.</c>, the two names curl reads as "upload from standard input".
    /// </summary>
    /// <param name="uploadFile">The argument given to <c>-T</c> / <c>--upload-file</c>.</param>
    /// <returns><see langword="true"/> for <c>-</c> or <c>.</c>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uploadFile"/> is <see langword="null"/>.</exception>
    public static bool IsStandardInput(string uploadFile)
    {
        ArgumentNullException.ThrowIfNull(uploadFile);

        return uploadFile is "-" or ".";
    }

    /// <summary>
    /// Decides whether and what to append the way curl's <c>add_file_name_to_url</c> does;
    /// parsing, validation (exit code 3) and normalisation of the URL are left to the URL
    /// layer. When the URL has no non-empty query and
    /// its path is absent or ends in <c>/</c>, returns the URL with the base name of
    /// <paramref name="uploadFile"/> (the text after its last <c>/</c> or <c>\</c>),
    /// percent-encoded as UTF-8, appended to the path; an absent path becomes <c>/</c>,
    /// an empty <c>?</c> is dropped, and a fragment is kept after the appended name.
    /// Returns <paramref name="url"/> unchanged when the upload is from standard input,
    /// when the query is non-empty, or when the path already names a file. The URL is
    /// split textually and not validated. A scheme is a letter followed by letters, digits,
    /// <c>+</c>, <c>.</c> or <c>-</c>, then <c>:</c> and one to three slashes, which are
    /// left as written; a URL without such a prefix is treated as host and path.
    /// </summary>
    /// <param name="url">The URL the upload is sent to.</param>
    /// <param name="uploadFile">The argument given to <c>-T</c> / <c>--upload-file</c>.</param>
    /// <returns>The URL with the local file name appended, or <paramref name="url"/> unchanged.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> or <paramref name="uploadFile"/> is <see langword="null"/>.
    /// </exception>
    public static string AppendLocalFileNameWhenUrlNamesNoFile(string url, string uploadFile)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(uploadFile);

        if (IsStandardInput(uploadFile))
        {
            return url;
        }

        UrlParts parts = UrlParts.Split(url);
        bool hasNonEmptyQuery = parts.FragmentStart - parts.QueryStart > 1;
        string path = url[parts.PathStart..parts.QueryStart];
        if (hasNonEmptyQuery || (path.Length > 0 && path[^1] != '/'))
        {
            return url;
        }

        string directoryPath = path.Length == 0 ? "/" : path;
        return string.Concat(
            url[..parts.PathStart],
            directoryPath,
            Uri.EscapeDataString(BaseName(uploadFile)),
            url[parts.FragmentStart..]);
    }

    private static string BaseName(string uploadFile) =>
        uploadFile[(uploadFile.LastIndexOfAny(['/', '\\']) + 1)..];

    /// <summary>
    /// Offsets splitting a URL into scheme and authority, path, query and fragment.
    /// Each part runs from its start to the next part's start.
    /// </summary>
    private readonly record struct UrlParts(int PathStart, int QueryStart, int FragmentStart)
    {
        public static UrlParts Split(string url)
        {
            int fragmentStart = StartOrEnd(url.IndexOf('#', StringComparison.Ordinal), url.Length);
            int queryStart = StartOrEnd(url.IndexOf('?', 0, fragmentStart), fragmentStart);
            int authorityStart = AuthorityStart(url);
            int pathStart = StartOrEnd(
                url.IndexOf('/', authorityStart, queryStart - authorityStart), queryStart);
            return new UrlParts(pathStart, queryStart, fragmentStart);
        }

        private static int StartOrEnd(int index, int end) => index < 0 ? end : index;

        // The authority starts after "scheme:" and up to three slashes; without a scheme
        // followed by at least one slash, the whole URL is host and path.
        private static int AuthorityStart(string url)
        {
            int colon = url.IndexOf(':', StringComparison.Ordinal);
            if (colon < 1 || !IsScheme(url.AsSpan(0, colon)))
            {
                return 0;
            }

            int slashes = Math.Min(CountLeadingSlashes(url.AsSpan(colon + 1)), MaximumSlashesAfterScheme);
            return slashes == 0 ? 0 : colon + 1 + slashes;
        }

        private static int CountLeadingSlashes(ReadOnlySpan<char> text) =>
            text.Length - text.TrimStart('/').Length;

        private static bool IsScheme(ReadOnlySpan<char> candidate) =>
            char.IsAsciiLetter(candidate[0]) && !candidate.ContainsAnyExcept(SchemeCharacters);
    }
}
