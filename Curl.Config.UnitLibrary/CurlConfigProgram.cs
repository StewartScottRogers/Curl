using Libcurl.Global;
using Libcurl.Misc.Version;

namespace Curl.Config;

/// <summary>
/// The <c>curl-config</c> executable's whole run (ADR-0712): reads the build from libcurl's
/// <c>curl_version_info</c> over a library it initialises and cleans up, then answers the options.
/// </summary>
public static class CurlConfigProgram
{
    /// <summary>Answers <paramref name="arguments"/> as <c>curl-config</c> does.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="isWindows">Whether the running system is Windows.</param>
    /// <param name="baseDirectory">The executable's folder, whose parent is the install prefix.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    public static Task<int> RunAsync(IReadOnlyList<string> arguments, TextWriter output, bool isWindows, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        CurlLibrary library = CurlGlobal.Init();
        int exitCode = CurlConfigCommand.Run(arguments, CurlConfigBuild.From(CurlVersion.Info(library), isWindows, CurlConfigBuild.PrefixOf(baseDirectory)), output);
        return library.DisposeAsync().AsTask().ContinueWith(_ => exitCode, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
