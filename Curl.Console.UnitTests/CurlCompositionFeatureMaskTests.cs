using System.Text;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the dialing <c>CreateRunner</c>'s run-time feature mask (ADR-0746, BL-2984): hiding
/// <see cref="EngineFeatureMask.Proxy" />, <c>-x wohoo http://example.com/</c> answers as a curl built
/// without proxy support does in upstream test375, and without the mask the same run is unchanged.
/// </summary>
[TestClass]
public sealed class CurlCompositionFeatureMaskTests
{
    private const string Hello = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private static readonly string[] ProxiedCommandLine = ["-x", "wohoo", "http://example.com/"];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CreateRunner_MaskHidingProxyAndProxyGiven_Exits4WithCurlsLineAndSendsNothing()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, EngineFeatureMask.Proxy, ProxiedCommandLine);

        Diagnostics.Assert("run.ExitCode", 4, run.ExitCode);
        Assert.AreEqual(4, run.ExitCode);
        Assert.AreEqual("curl: proxy support is disabled in this libcurl" + (OperatingSystem.IsWindows() ? "\r\n" : "\n"), run.StandardError);
        Assert.IsEmpty(run.StandardOutput);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task CreateRunner_MaskHidingProxyAndNoProxyGiven_TransfersDirectly()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, EngineFeatureMask.Proxy, "http://example.com/");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual("hello", run.StandardOutput);
        Assert.IsEmpty(run.StandardError);
        Assert.AreEqual(80, server.Targets.Single().Port);
    }

    [TestMethod]
    public async Task CreateRunner_NoMaskAndProxyGiven_SendsTheRequestToTheProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunAsync(server, EngineFeatureMask.None, ProxiedCommandLine);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Assert.AreEqual("hello", run.StandardOutput);
        Assert.IsEmpty(run.StandardError);
        Assert.StartsWith("GET http://example.com/ HTTP/1.1\r\n", Latin1(server.Written));
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private async Task<Run> RunAsync(ScriptedConnector server, EngineFeatureMask featureMask, params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        Diagnostics.ArrangeCommandLine(arguments);
        Diagnostics.Arrange("featureMask", featureMask.ToString());
        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                new ScriptedTcpDialer(server),
                new LoopbackDnsResolver(),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                featureMask: featureMask)
            .RunAsync(arguments);

        Run run = new(exitCode, Latin1(standardOutput.ToArray()), Latin1(standardError.ToArray()));
        Diagnostics.ActRun(run.ExitCode, run.StandardOutput, run.StandardError);
        return run;
    }

    private sealed record Run(int ExitCode, string StandardOutput, string StandardError);
}
