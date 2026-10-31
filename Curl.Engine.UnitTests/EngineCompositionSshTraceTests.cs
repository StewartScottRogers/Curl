using Curl.Console;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[SSH]</c> lines - <c>ssh</c>,
/// <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - and that the composition hands that choice
/// to the SSH handler (BL-1166). The lines themselves are pinned in
/// <c>Curl.Original.Protocol.Ssh.UnitTests</c>; the console writes them only under <c>-v</c>, as it writes
/// every info line.
/// </summary>
[TestClass]
public sealed class EngineCompositionSshTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-v", "--trace-config", "ssh")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,SSH")]
    [DataRow("-vv")]
    public void TracesSsh_WithTheSshComponent_IsTrue(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool tracesSsh = EngineComposition.TracesSsh(EasyOptionsFromCommandLine.TraceComponentsOf(Parse(arguments)));

        Diagnostics.Act("traces ssh", tracesSsh);
        Diagnostics.Assert("traces ssh", true, tracesSsh);
        Assert.IsTrue(tracesSsh);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ftp")]
    [DataRow("-v", "--trace-config", "ssh,-ssh")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesSsh_WithoutTheSshComponent_IsFalse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool tracesSsh = EngineComposition.TracesSsh(EasyOptionsFromCommandLine.TraceComponentsOf(Parse(arguments)));

        Diagnostics.Act("traces ssh", tracesSsh);
        Diagnostics.Assert("traces ssh", false, tracesSsh);
        Assert.IsFalse(tracesSsh);
    }

    [TestMethod]
    public void CreateTransports_UnderTraceConfigSsh_TracesSsh()
    {
        Diagnostics.Arrange("arguments", "-v --trace-config ssh");

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse("-v", "--trace-config", "ssh")), EasyOptionsFromCommandLine.NetworkOf(Parse("-v", "--trace-config", "ssh")), EasyOptionsFromCommandLine.TlsOf(Parse("-v", "--trace-config", "ssh")), EasyOptionsFromCommandLine.HttpOf(Parse("-v", "--trace-config", "ssh")), EasyOptionsFromCommandLine.ProxyOf(Parse("-v", "--trace-config", "ssh")), EasyOptionsFromCommandLine.AuthenticationOf(Parse("-v", "--trace-config", "ssh")), TimeProvider.System, traces: EasyOptionsFromCommandLine.TraceComponentsOf(Parse("-v", "--trace-config", "ssh")));

        Diagnostics.Act("transports traces ssh", transports.TracesSsh);
        Diagnostics.Assert("transports traces ssh", true, transports.TracesSsh);
        Assert.IsTrue(transports.TracesSsh);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheSshComponent_DoesNotTraceSsh()
    {
        Diagnostics.Arrange("arguments", "-v");

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse("-v")), EasyOptionsFromCommandLine.NetworkOf(Parse("-v")), EasyOptionsFromCommandLine.TlsOf(Parse("-v")), EasyOptionsFromCommandLine.HttpOf(Parse("-v")), EasyOptionsFromCommandLine.ProxyOf(Parse("-v")), EasyOptionsFromCommandLine.AuthenticationOf(Parse("-v")), TimeProvider.System, traces: EasyOptionsFromCommandLine.TraceComponentsOf(Parse("-v")));

        Diagnostics.Act("transports traces ssh", transports.TracesSsh);
        Diagnostics.Assert("transports traces ssh", false, transports.TracesSsh);
        Assert.IsFalse(transports.TracesSsh);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheSshHandler(bool tracesSsh)
    {
        Diagnostics.Arrange("traces ssh requested", tracesSsh);

        SshProtocolHandler ssh = EngineComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesSsh: tracesSsh)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<SshProtocolHandler>()
            .Single();

        Diagnostics.Act("handler traces state machine", ssh.TracesStateMachine);
        Diagnostics.Assert("handler traces state machine", tracesSsh, ssh.TracesStateMachine);
        Assert.AreEqual(tracesSsh, ssh.TracesStateMachine);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "sftp://127.0.0.1/f"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
