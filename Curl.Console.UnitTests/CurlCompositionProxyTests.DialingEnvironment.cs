using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the dialing <c>CreateRunner</c>'s environment reader (BL-1928): the proxy variables it
/// reads choose the run's proxy, as the process's do for the executable, and a run given no
/// reader reads none, so the upstream case runner can give one run its <c>&lt;setenv&gt;</c>
/// variables without touching the process environment.
/// </summary>
public sealed partial class CurlCompositionProxyTests
{
    private const string DirectGet = "GET /a HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    [TestMethod]
    public async Task CreateRunner_DialingRunGivenHttpProxy_SendsTheRequestToTheProxy()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunDialingAsync(server, new() { ["http_proxy"] = "http://127.0.0.1:18238" });

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("server.Targets.Single().Port", 18238, server.Targets.Single().Port);
        Assert.AreEqual(18238, server.Targets.Single().Port);
        Diagnostics.Assert("Latin1(server.Written)", ForwardedGet, Latin1(server.Written));
        Assert.AreEqual(ForwardedGet, Latin1(server.Written));
    }

    [TestMethod]
    public async Task CreateRunner_DialingRunGivenHttpProxyAndNoProxyForTheHost_SendsTheRequestToTheHost()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunDialingAsync(server, new() { ["http_proxy"] = "http://127.0.0.1:18238", ["no_proxy"] = "example.com" });

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("server.Targets.Single().Port", 80, server.Targets.Single().Port);
        Assert.AreEqual(80, server.Targets.Single().Port);
        Diagnostics.Assert("Latin1(server.Written)", DirectGet, Latin1(server.Written));
        Assert.AreEqual(DirectGet, Latin1(server.Written));
    }

    [TestMethod]
    public async Task CreateRunner_DialingRunGivenNoEnvironmentReader_SendsTheRequestToTheHost()
    {
        ScriptedConnector server = new([Latin1(Hello)]);

        Run run = await RunDialingAsync(server, environment: null);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("server.Targets.Single().Port", 80, server.Targets.Single().Port);
        Assert.AreEqual(80, server.Targets.Single().Port);
        Diagnostics.Assert("Latin1(server.Written)", DirectGet, Latin1(server.Written));
        Assert.AreEqual(DirectGet, Latin1(server.Written));
    }

    private async Task<Run> RunDialingAsync(ScriptedConnector server, Dictionary<string, string>? environment)
    {
        string[] arguments = ["-sS", "http://example.com/a"];
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        Diagnostics.ArrangeCommandLine(arguments);
        Diagnostics.Arrange("environment", environment is null ? "(no reader)" : string.Join(", ", environment.Select(variable => $"{variable.Key}={variable.Value}")));
        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                new ScriptedTcpDialer(server),
                new LoopbackDnsResolver(),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                readEnvironmentVariable: environment is null ? null : name => environment.GetValueOrDefault(name))
            .RunAsync(arguments);

        Run run = new(exitCode, Latin1(standardOutput.ToArray()), Latin1(standardError.ToArray()));
        Diagnostics.ActRun(run.ExitCode, run.StandardOutput, run.StandardError);
        return run;
    }
}
