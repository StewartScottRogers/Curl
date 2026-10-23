namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Finds the two binaries a differential run compares: the reference curl named by
/// <c>CURL_DIFFERENTIAL_REFERENCE</c> (ADR-0782) and the Curl under test, named by
/// <c>CURL_DIFFERENTIAL_OURS</c> or else the <c>Curl.Console</c> build in this project's
/// output (ADR-0785).
/// </summary>
internal static class DifferentialBinaries
{
    /// <summary>The environment variable naming the reference curl 8.22.0 binary.</summary>
    public const string ReferenceVariable = "CURL_DIFFERENTIAL_REFERENCE";

    /// <summary>The environment variable naming the Curl binary under test, when not the built one.</summary>
    public const string OursVariable = "CURL_DIFFERENTIAL_OURS";

    /// <summary>Gets the reference binary's path, or <see langword="null"/> when none is named.</summary>
    public static string? FindReference() =>
        Environment.GetEnvironmentVariable(ReferenceVariable) is { Length: > 0 } path ? path : null;

    /// <summary>Gets the command that starts the Curl under test, before the case's arguments.</summary>
    public static IReadOnlyList<string> FindOurs()
    {
        if (Environment.GetEnvironmentVariable(OursVariable) is { Length: > 0 } path)
        {
            return [path];
        }

        var appHost = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "curl.exe" : "curl");
        return File.Exists(appHost) ? [appHost] : ["dotnet", Path.Combine(AppContext.BaseDirectory, "curl.dll")];
    }
}
