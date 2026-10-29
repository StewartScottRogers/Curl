using System.Text;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-242: a refused CONNECT is past connect, so the progress meter's header and a zero
/// row come before curl's error line, as curl 8.22.0 writes them (measured on Windows,
/// 2026-10-29, BL-3771).
/// </summary>
[TestClass]
public sealed class Cgr242Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_ProxyTunnelRefusedWith403_WritesTheMeterBeforeTheError()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n")]);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        string[] arguments = ["-o", "/dev/null", "-p", "-x", "127.0.0.1:18238", "http://example.com/a"];
        Diagnostics.ArrangeCommandLine(arguments);

        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                new TcpConnector(new LoopbackDnsResolver(), new ScriptedTcpDialer(server), new PassThroughTlsProvider(), TimeProvider.System, EngineProxyTunnelOptions()),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                writesProgressMeter: true)
            .RunAsync(arguments);

        string standardErrorText = Encoding.Latin1.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        string expected =
            "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current\n"
            + "                                 Dload  Upload  Total   Spent   Left   Speed\n"
            + "\r  0      0   0      0   0      0      0      0                              0\n"
            + "curl: (7) CONNECT tunnel failed, response 403\n";
        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Diff("stderr", expected, standardErrorText);
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(expected, standardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SmtpRecipientRefused_WritesTheMeterBeforeTheError()
    {
        ScriptedConnector server = new(
        [
            Encoding.Latin1.GetBytes("220 hi\r\n"),
            Encoding.Latin1.GetBytes("250 hi\r\n"),
            Encoding.Latin1.GetBytes("250 ok\r\n"),
            Encoding.Latin1.GetBytes("550 no such user\r\n"),
        ]);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new(Encoding.Latin1.GetBytes("body\r\n"));
        string[] arguments = ["--mail-from", "sender@example.com", "--mail-rcpt", "bad@example.com", "-T", "-", "smtp://127.0.0.1:18239/907"];
        Diagnostics.ArrangeCommandLine(arguments);

        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                new TcpConnector(new LoopbackDnsResolver(), new ScriptedTcpDialer(server), new PassThroughTlsProvider(), TimeProvider.System, null),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                writesProgressMeter: true)
            .RunAsync(arguments);

        string standardErrorText = Encoding.Latin1.GetString(standardError.ToArray());
        Diagnostics.Assert("exit code", 55, exitCode);
        Diagnostics.Assert("stderr", "meter header first", standardErrorText);
        Assert.AreEqual(55, exitCode);
        StringAssert.StartsWith(standardErrorText, "  % Total    % Received % Xferd");
        StringAssert.EndsWith(standardErrorText, "curl: (55) RCPT failed: 550" + Environment.NewLine);
    }
    private static HttpProxyTunnelOptions? EngineProxyTunnelOptions() => null;
}
