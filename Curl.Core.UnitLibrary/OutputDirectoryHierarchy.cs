namespace Curl.Core;

/// <summary>
/// Gives the directories <c>--create-dirs</c> creates for an output file's path, in order, as
/// curl 8.22.0's <c>create_dir_hierarchy</c> (<c>src/tool_dirhie.c</c>) passes them to
/// <c>mkdir</c>: each leading part of the path that ends just before a separator, with the
/// separators before it, the file's own name excepted.
/// </summary>
/// <remarks>
/// <para>
/// A run of separators belongs to the name after it, so <c>/a//b/c.txt</c> gives <c>/a</c> and
/// <c>/a//b</c>, never an empty or separator-ended part; a trailing separator names no file,
/// so <c>foo/bar/</c> gives <c>foo</c> and <c>foo/bar</c>.
/// </para>
/// <para>
/// With the Windows rule set <c>\</c> separates directories as well as <c>/</c>, and a first
/// part of exactly two characters whose second is <c>:</c> (a drive such as <c>C:</c>) is
/// skipped, while a drive-relative <c>C:foo</c> is still given. The rule set is a parameter
/// so both run on every operating system, as <c>tests/tunit/tool1720.c</c> checks them.
/// </para>
/// </remarks>
public static class OutputDirectoryHierarchy
{
    /// <summary>
    /// Gives each directory curl would try to create for <paramref name="outputFile" />, in
    /// order. The sequence is lazy, so a caller that stops at a failure reads no further.
    /// </summary>
    /// <param name="outputFile">The output file's path, as it will be opened.</param>
    /// <param name="runsOnWindows">Whether the Windows rule set applies: <c>\</c> as a separator and a leading drive skipped.</param>
    /// <returns>The leading directories, shortest first; none for a bare file name or an empty path.</returns>
    public static IEnumerable<string> DirectoriesToCreate(string outputFile, bool runsOnWindows)
    {
        string separators = runsOnWindows ? "\\/" : "/";

        for (int start = 0; ;)
        {
            int nameStart = IndexOrLength(outputFile, outputFile.AsSpan(start).IndexOfAnyExcept(separators), start);
            int nameEnd = IndexOrLength(outputFile, outputFile.AsSpan(nameStart).IndexOfAny(separators), nameStart);
            if (nameEnd == outputFile.Length)
            {
                yield break;
            }

            if (!(runsOnWindows && start == 0 && IsDrive(outputFile, nameStart, nameEnd)))
            {
                yield return outputFile[..nameEnd];
            }

            start = nameEnd;
        }
    }

    /// <summary>
    /// Turns an index found in a span starting at <paramref name="offset" /> into an index in
    /// <paramref name="path" />, or its length when nothing was found.
    /// </summary>
    /// <param name="path">The whole path.</param>
    /// <param name="spanIndex">The index in the span, or -1.</param>
    /// <param name="offset">Where the span starts in <paramref name="path" />.</param>
    /// <returns>The index in <paramref name="path" />.</returns>
    private static int IndexOrLength(string path, int spanIndex, int offset) =>
        spanIndex < 0 ? path.Length : offset + spanIndex;

    /// <summary>
    /// Tells whether the first part names a bare drive, as curl tests it: the name is two
    /// characters long and the path's second character is <c>:</c>.
    /// </summary>
    /// <param name="path">The whole path.</param>
    /// <param name="nameStart">Where the first part's name starts.</param>
    /// <param name="nameEnd">Where the first part's name ends.</param>
    /// <returns><see langword="true" /> when the part is skipped.</returns>
    private static bool IsDrive(string path, int nameStart, int nameEnd) =>
        nameEnd - nameStart == 2 && path[1] == ':';
}
