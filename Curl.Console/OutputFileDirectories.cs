using Curl.Core;
using Curl.Engine;
namespace Curl.Console;

/// <summary>
/// Creates the directories an output file's path names, for <c>--create-dirs</c>, as curl
/// 8.22.0's <c>create_dir_hierarchy</c> does: each directory
/// <see cref="OutputDirectoryHierarchy.DirectoriesToCreate" /> gives, in order.
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
        foreach (string directory in OutputDirectoryHierarchy.DirectoriesToCreate(outputFile, runsOnWindows))
        {
            if (CreateDirectory(outputPaths, directory) is { } failure)
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
    /// <returns>
    /// The failure, or <see langword="null" /> when it exists now, or it failed with
    /// <c>EACCES</c> or <c>EEXIST</c>.
    /// </returns>
    private static DirectoryCreationFailure? CreateDirectory(IOutputPaths outputPaths, string directory) =>
        outputPaths.TryCreateDirectory(directory, out int errorNumber)
        || errorNumber is CRuntimeErrorNumbers.PermissionDenied or CRuntimeErrorNumbers.AlreadyExists
            ? null
            : new DirectoryCreationFailure(directory, errorNumber);
}
