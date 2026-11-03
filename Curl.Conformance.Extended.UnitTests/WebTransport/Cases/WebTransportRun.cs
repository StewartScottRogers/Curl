using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.WebTransport.Server;
using Curl.Conformance.Extended.WebTransport.Server;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Cases;

/// <summary>
/// Runs <c>curl -sS -k -m 10</c> for <c>webtransport://localhost/chat</c> (UDP 443) in process against a
/// <see cref="WebTransportReferenceServer" />, so a case reads what curl wrote and what the server recorded.
/// </summary>
internal static class WebTransportRun
{
    public const string Url = "webtransport://localhost/chat";

    public const int Port = 443;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    public static Task CaseAsync(IReadOnlyList<string> requirementIds, Func<Task> runCase) => Known.RunCaseAsync(requirementIds, runCase);

    public static WebTransportReferenceServer Server(WebTransportServerScript script) => WebTransportTestClient.CreateServer(script);

    public static Task<CurlRunResult> RunAsync(WebTransportReferenceServer server, string url, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(Port, server)], ["-sS", "-k", "-m", "10", .. arguments, url]);

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>Asserts the run succeeded with <paramref name="standardOutput" /> and nothing on stderr.</summary>
    public static void AssertSucceeded(CurlRunResult result, string standardOutput)
    {
        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(standardOutput, Text(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
    }

    /// <summary>Asserts the run failed with <paramref name="exitCode" />, wrote nothing to stdout and <c>curl: (code) message</c> to stderr.</summary>
    public static void AssertFailed(CurlRunResult result, int exitCode, string message)
    {
        Assert.AreEqual(exitCode, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.AreEqual($"curl: ({exitCode}) {message}\n".Replace("\n", Environment.NewLine, StringComparison.Ordinal), Text(result.StandardError));
    }

    /// <summary>Gets the value of the client's setting <paramref name="identifier" />, or null when its SETTINGS leave it out.</summary>
    public static long? ClientSetting(WebTransportReferenceServer server, long identifier) =>
        server.ClientSettings!.Where(setting => setting.Identifier == identifier).Select(setting => (long?)setting.Value).FirstOrDefault();

    /// <summary>Gets the CONNECT field named <paramref name="name" />.</summary>
    public static string? ConnectField(WebTransportReferenceServer server, string name) =>
        server.ConnectFields!.Where(field => field.Name == name).Select(field => field.Value).FirstOrDefault();
}
