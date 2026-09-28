namespace Curl.Console;

/// <summary>
/// Creates the directories <c>--create-dirs</c> makes for an output file, and tells whether an
/// output file <c>--skip-existing</c> would skip is already there. The seam that keeps them off
/// the disk in tests.
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
}
