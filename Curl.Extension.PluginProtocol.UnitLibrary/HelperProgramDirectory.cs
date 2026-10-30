using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.PluginProtocol;

/// <summary>Lists a <c>PATH</c> directory's helper program files on the real file system.</summary>
internal static class HelperProgramDirectory
{
    /// <summary>Lists the paths of <paramref name="directory" />'s <c>curl-proto-*</c> files.</summary>
    /// <param name="directory">One <c>PATH</c> entry.</param>
    /// <returns>The files, or none when the directory is missing or cannot be read, as a shell skips it.</returns>
    // Excluded per ADR-0083: its catch needs a directory the file system refuses to list, which no unit test can make on every platform.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin file system adapter; its catch needs an unreadable directory.")]
    internal static IEnumerable<string> ListHelperFiles(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? [.. Directory.EnumerateFiles(directory, HelperProgramSearch.ProgramPrefix + "*")] : [];
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
