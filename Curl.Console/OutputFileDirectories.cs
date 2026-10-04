namespace Curl.Console;

/// <summary>
/// Creates the directories an output file's path names, for <c>--create-dirs</c>, as curl
/// 8.21.0's <c>create_dir_hierarchy</c> does: each leading part of the path up to a
/// separator, in order, the file's own name excepted.
/// </summary>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-239 Notes):
/// <c>--create-dirs -o a/b/c.txt</c> creates <c>a</c> and <c>a/b</c>; <c>x\y\z.txt</c> creates
/// <c>x</c> and <c>x\y</c>; <c>--output-dir n1/n2 --create-dirs -O</c> creates <c>n1</c> and
/// <c>n1/n2</c>. With a file <c>blk</c> in the way, <c>-o blk/b/c.txt</c> fails on <c>blk/b</c>.
/// </remarks>
internal static class OutputFileDirectories
{
    /// <summary>
    /// Creates every directory <paramref name="outputFile" /> lies in that does not exist.
    /// </summary>
    /// <param name="outputPaths">Checks and creates the directories.</param>
    /// <param name="outputFile">The output file's path, as it will be opened.</param>
    /// <param name="runsOnWindows">Whether <c>\</c> separates directories as well as <c>/</c>.</param>
    /// <returns>
    /// The first directory that could not be created, as a leading part of
    /// <paramref name="outputFile" />, with its <c>errno</c>; <see langword="null" /> when every
    /// directory exists now or failed only with <c>EACCES</c> or <c>EEXIST</c>, which curl
    /// ignores so traversal of an unreadable or existing parent goes on (BL-1433).
    /// </returns>
    internal static DirectoryCreationFailure? CreateLeadingDirectories(IOutputPaths outputPaths, string outputFile, bool runsOnWindows)
    {
        char[] separators = runsOnWindows ? ['/', '\\'] : ['/'];

        for (int end = outputFile.IndexOfAny(separators); end >= 0; end = outputFile.IndexOfAny(separators, end + 1))
        {
            if (CreateDirectory(outputPaths, outputFile[..end], separators) is { } failure)
            {
                return failure;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates one leading directory, as one pass of curl's loop does.
    /// </summary>
    /// <param name="outputPaths">Checks and creates the directory.</param>
    /// <param name="directory">The leading part of the path.</param>
    /// <param name="separators">The directory separators.</param>
    /// <returns>
    /// The failure, or <see langword="null" /> when there was nothing to create, it exists now,
    /// or it failed with <c>EACCES</c> or <c>EEXIST</c>.
    /// </returns>
    private static DirectoryCreationFailure? CreateDirectory(IOutputPaths outputPaths, string directory, char[] separators) =>
        IsRootOrRepeatedSeparator(directory, separators)
        || outputPaths.TryCreateDirectory(directory, out int errorNumber)
        || errorNumber is CRuntimeErrorNumbers.PermissionDenied or CRuntimeErrorNumbers.AlreadyExists
            ? null
            : new DirectoryCreationFailure(directory, errorNumber);

    /// <summary>
    /// Tells whether a leading part of the path names no directory to create: it is empty,
    /// the part before a leading separator, or ends in a separator, from a doubled one.
    /// </summary>
    /// <param name="directory">The leading part.</param>
    /// <param name="separators">The directory separators.</param>
    /// <returns><see langword="true" /> when there is nothing to create.</returns>
    private static bool IsRootOrRepeatedSeparator(string directory, char[] separators) =>
        directory.Length == 0 || separators.Contains(directory[^1]);
}
