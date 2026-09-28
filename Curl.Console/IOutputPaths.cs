namespace Curl.Console;

/// <summary>
/// Creates the directories <c>--create-dirs</c> makes for an output file. The seam that keeps
/// them off the disk in tests.
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
}
