namespace Curl.Api;

/// <summary>
/// Reads and writes the text files behind the stores: a file's lines without its comments and
/// blank lines, and a file written as curl 8.22.0 writes it, its header then one entry per line.
/// </summary>
internal static class StoreFile
{
    /// <summary>The lines of <paramref name="path"/> that are neither blank nor comments (<c>#</c>), but for <paramref name="keptCommentPrefix"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="keptCommentPrefix">A <c>#</c> prefix that marks an entry rather than a comment, such as <c>#HttpOnly_</c>; <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The entry lines, in file order.</returns>
    internal static async Task<IReadOnlyList<string>> ReadEntryLinesAsync(string path, string? keptCommentPrefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        return [.. lines.Where(line => IsEntry(line, keptCommentPrefix))];
    }

    /// <summary>Writes <paramref name="header"/>, then <paramref name="entries"/>, each line ending in <c>\n</c> as curl's files do.</summary>
    /// <param name="path">The file, replaced if it exists.</param>
    /// <param name="header">The header lines.</param>
    /// <param name="entries">The entry lines.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    internal static Task WriteAsync(string path, IEnumerable<string> header, IEnumerable<string> entries, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string text = string.Concat(header.Concat(entries).Select(line => line + "\n"));
        return File.WriteAllTextAsync(path, text, cancellationToken);
    }

    private static bool IsEntry(string line, string? keptCommentPrefix) =>
        line.Trim().Length > 0
        && (!line.StartsWith('#') || (keptCommentPrefix is not null && line.StartsWith(keptCommentPrefix, StringComparison.Ordinal)));
}
