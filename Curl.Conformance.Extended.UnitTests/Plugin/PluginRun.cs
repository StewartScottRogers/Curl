using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Plugin;

/// <summary>Runs curl in process with helper files in a temporary directory and a stand-in helper behind them (ADR-1142).</summary>
internal static class PluginRun
{
    public const string Url = "demo://example/item";

    public static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>The <c>curl-proto-demo</c> file name the platform runs: with a <c>PATHEXT</c> extension on Windows.</summary>
    public static string DemoName => ProgramFileName("demo");

    public static string ProgramFileName(string scheme) => "curl-proto-" + scheme + (OperatingSystem.IsWindows() ? ".cmd" : string.Empty);

    public static Dictionary<string, string> PathOf(params string[] directories) =>
        new() { ["PATH"] = string.Join(Path.PathSeparator, directories) };

    public static Task<CurlRunResult> RunAsync(PluginReferenceHelperStarter starter, IReadOnlyDictionary<string, string>? environment, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [], ["-sS", .. arguments], starter, environment);

    /// <summary>Runs <paramref name="arguments" /> with one directory on <c>PATH</c> holding <c>curl-proto-demo</c>, answered by <paramref name="helper" />.</summary>
    public static async Task<(CurlRunResult Result, PluginReferenceHelperStarter Starter, string Program)> RunDemoAsync(PluginReferenceHelper? helper, params string[] arguments)
    {
        using HelperDirectory directory = new();
        string program = directory.Add(DemoName);
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), helper);
        CurlRunResult result = await RunAsync(starter, null, arguments);
        return (result, starter, program);
    }

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>A temporary directory of helper files, deleted with everything in it.</summary>
    internal sealed class HelperDirectory : IDisposable
    {
        public string FullName { get; } = Directory.CreateTempSubdirectory("curl-plugin-").FullName;

        public string Add(string fileName)
        {
            string path = Path.Combine(FullName, fileName);
            File.WriteAllBytes(path, []);
            return path;
        }

        public void Dispose() => Directory.Delete(FullName, recursive: true);
    }
}
