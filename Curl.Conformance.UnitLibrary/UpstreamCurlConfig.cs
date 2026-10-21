using Curl.Config;
using Libcurl.Global;
using Libcurl.Misc.Version;

namespace Curl.Conformance;

/// <summary>
/// Stands in for <c>../curl-config</c>, which upstream's <c>test1013.pl</c> and <c>test1022.pl</c> run
/// (ADR-0712): the lines Curl's <c>curl-config</c> prints for one option, from the same
/// <see cref="CurlConfigCommand"/> the executable runs, over libcurl's <c>curl_version_info</c> on the
/// platform the tests run on.
/// </summary>
internal static class UpstreamCurlConfig
{
    // Initialised once for the test process's life, as a program calls curl_global_init once.
    private static readonly CurlLibrary Library = CurlGlobal.Init();

    private static readonly CurlConfigBuild Build = CurlConfigBuild.From(CurlVersion.Info(Library), OperatingSystem.IsWindows(), "");

    /// <summary>The lines <c>curl-config</c> prints for <paramref name="option"/>, without their line feeds.</summary>
    /// <param name="option">The option, such as <c>--protocols</c>.</param>
    /// <returns>The lines.</returns>
    public static string[] Lines(string option)
    {
        using StringWriter output = new();
        _ = CurlConfigCommand.Run([option], Build, output);
        return output.ToString().Split('\n')[..^1];
    }
}
