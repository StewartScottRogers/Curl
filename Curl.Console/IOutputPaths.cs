namespace Curl.Console;

/// <summary>
/// Checks and creates the paths an output file lands at: whether a <c>-J</c> name is already
/// taken, and the directories <c>--create-dirs</c> makes. The seam that keeps both off the disk
/// in tests.
/// </summary>
internal interface IOutputPaths
{
    /// <summary>
    /// Tells whether a file, not a directory, exists at <paramref name="path" />.
    /// </summary>
    /// <param name="path">The path, as the runner will open it.</param>
    /// <returns><see langword="true" /> when a file exists there.</returns>
    bool FileExists(string path);

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
}
