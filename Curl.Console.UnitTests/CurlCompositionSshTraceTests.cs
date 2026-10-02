using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[SSH]</c> lines - <c>ssh</c>,
/// <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - and that the composition hands that choice
/// to the SSH handler (BL-1166). The lines themselves are pinned in
/// <c>Curl.Protocol.Ssh.UnitTests</c>; the console writes them only under <c>-v</c>, as it writes
/// every info line.
/// </summary>
[TestClass]
public sealed class CurlCompositionSshTraceTests
{
    [TestMethod]
    [DataRow("-v", "--trace-config", "ssh")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,SSH")]
    [DataRow("-vv")]
    public void TracesSsh_WithTheSshComponent_IsTrue(params string[] arguments) =>
        Assert.IsTrue(CurlComposition.TracesSsh(Parse(arguments)));

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ftp")]
    [DataRow("-v", "--trace-config", "ssh,-ssh")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesSsh_WithoutTheSshComponent_IsFalse(params string[] arguments) =>
        Assert.IsFalse(CurlComposition.TracesSsh(Parse(arguments)));

    [TestMethod]
    public void CreateTransports_UnderTraceConfigSsh_TracesSsh()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "ssh"), TimeProvider.System);

        Assert.IsTrue(transports.TracesSsh);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheSshComponent_DoesNotTraceSsh()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);

        Assert.IsFalse(transports.TracesSsh);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheSshHandler(bool tracesSsh)
    {
        SshProtocolHandler ssh = CurlComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesSsh: tracesSsh)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<SshProtocolHandler>()
            .Single();

        Assert.AreEqual(tracesSsh, ssh.TracesStateMachine);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "sftp://127.0.0.1/f"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
