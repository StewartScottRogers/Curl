namespace Curl.Console;

/// <summary>
/// Creates the directories <c>--create-dirs</c> makes for an output file, tells whether an
/// output file <c>--skip-existing</c> would skip is already there, and deletes the output file
/// of a transfer <c>--remove-on-error</c> saw fail. The seam that keeps them off the disk in tests.
/// </summary>
internal interface IOutputPaths
{
    /// <summary>
    /// Makes sure <paramref name="path" /> exists, creating it as a directory when nothing is
    /// there, as curl's <c>mkdir</c> taking <c>EEXIST</c> as success does.
    /// </summary>
    /// <param name="path">The directory.</param>
    /// <returns>
    /// <see langword="true" /> when something already existed there or the directory was
    /// created; <see langword="false" /> when it could not be created.
    /// </returns>
    bool TryCreateDirectory(string path);

    /// <summary>
    /// Tells whether anything, a file or a directory, is at <paramref name="path" />, as curl's
    /// <c>stat</c> succeeding does.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true" /> when something is there.</returns>
    bool Exists(string path);

    /// <summary>
    /// Deletes the <c>--remove-on-error</c> output file at <paramref name="path" /> when it is a
    /// regular file, as curl 8.21.0's <c>stat</c>, <c>S_ISREG</c> and <c>unlink</c> do.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>
    /// <see cref="OutputFileRemoval.Removed" /> when the file is gone;
    /// <see cref="OutputFileRemoval.NotRegularFile" /> off Windows when no regular file was there
    /// (<c>/dev/null</c>, a directory, nothing); <see cref="OutputFileRemoval.Failed" /> when it
    /// could not be deleted, and on Windows when no file was there (<c>NUL</c>, a directory, nothing).
    /// </returns>
    OutputFileRemoval RemoveFile(string path);
}
