using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery;

/// <summary>
/// Runs one discovery-surface case: curl in process with extensions on (an empty environment) or off
/// (<c>CURL_EXTENSIONS=0</c>), and reads what <c>--extended-help</c> says of each extension.
/// </summary>
internal static class DiscoveryCaseRun
{
    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    /// <summary>Gets the environment with extensions on: nothing set.</summary>
    internal static IReadOnlyDictionary<string, string> On { get; } = new Dictionary<string, string>();

    /// <summary>Gets the environment with extensions off: <c>CURL_EXTENSIONS=0</c>.</summary>
    internal static IReadOnlyDictionary<string, string> Off { get; } = new Dictionary<string, string> { ["CURL_EXTENSIONS"] = "0" };

    /// <summary>
    /// Gets every extended protocol library's extension name and its schemes, as each library's <c>*Extension.cs</c>
    /// declares them. The Plugin extension has no fixed scheme; its helper schemes are BL-4489's.
    /// </summary>
    internal static IReadOnlyDictionary<string, string[]> ExtendedProtocols { get; } = new Dictionary<string, string[]>
    {
        ["amqp0"] = ["amqp", "amqps"],
        ["amqp1"] = ["amqp1", "amqp1s"],
        ["azure-storage"] = ["az", "abfs", "abfss"],
        ["coap"] = ["coap", "coaps"],
        ["dns"] = ["dns"],
        ["gs"] = ["gs"],
        ["grpc"] = ["grpc", "grpcs"],
        ["kafka"] = ["kafka", "kafkas"],
        ["mcp"] = ["mcp", "mcps", "mcp+stdio"],
        ["mqtt5"] = ["mqtt5", "mqtt5s", "mqtt5+ws"],
        ["nats"] = ["nats", "natss"],
        ["oci"] = ["oci"],
        ["plugin-protocol"] = [],
        ["redis"] = ["redis", "rediss"],
        ["s3"] = ["s3"],
        ["signalr"] = ["signalr", "signalrs"],
        ["socketio"] = ["socketio", "socketios"],
        ["stomp"] = ["stomp", "stomps"],
        ["webtransport"] = ["webtransport"],
        ["xmpp"] = ["xmpp", "xmpps"],
    };

    /// <summary>Gets the known failures every discovery case runs under.</summary>
    internal static KnownFailures Known { get; } = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>Runs curl with <paramref name="arguments" /> in <paramref name="environment" />; no server is reachable.</summary>
    internal static Task<CurlRunResult> RunAsync(IReadOnlyDictionary<string, string> environment, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(CurlRunner, [], arguments, environment);

    /// <summary>The run's standard output as text.</summary>
    internal static string OutputOf(CurlRunResult result) => Encoding.UTF8.GetString(result.StandardOutput);

    /// <summary>The run's standard error as text.</summary>
    internal static string ErrorOf(CurlRunResult result) => Encoding.UTF8.GetString(result.StandardError);

    /// <summary>The lines <c>curl --extended-help</c> prints with extensions on.</summary>
    internal static async Task<IReadOnlyList<ExtendedHelpLine>> ExtendedHelpLinesAsync()
    {
        CurlRunResult result = await RunAsync(On, "--extended-help");
        Assert.AreEqual(0, result.ExitCode, "--extended-help exit code");
        return [.. OutputOf(result).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => ExtendedHelpLine.Parse(line.TrimEnd('\r')))];
    }

    /// <summary>Every scheme <c>--extended-help</c> lists, in its order, each once.</summary>
    internal static async Task<IReadOnlyList<string>> ListedSchemesAsync() =>
        [.. (await ExtendedHelpLinesAsync()).SelectMany(line => line.Schemes).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Fails with every entry of <paramref name="failures" /> when there is any.</summary>
    internal static void AssertNone(string requirement, IReadOnlyCollection<string> failures) =>
        Assert.IsEmpty(failures, $"{requirement}:\n{string.Join("\n", failures)}");
}
