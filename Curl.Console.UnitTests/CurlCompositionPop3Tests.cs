using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>pop3://</c> and <c>pop3s://</c> end to end through the production composition over
/// fake connectors: the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers
/// and the SASL authenticator it composes. Every exchange was recorded from curl 8.21.0 (mingw,
/// Schannel) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Pop3</c> (BL-548 and BL-549 Notes):
/// curl running <c>-sS [-u u:p] pop3://127.0.0.1:18110/1</c> against the recorder's default
/// greeting and <c>CAPA</c> list, which offers <c>SASL PLAIN LOGIN</c>, so <c>-u u:p</c> logs in
/// with <c>AUTH PLAIN</c>. The recorder sends each reply in its own write, so each is its own read.
/// </summary>
[TestClass]
public sealed class CurlCompositionPop3Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Message = "Subject: a\r\n\r\nline one\r\n..two dots\r\n.one dot\r\nlast\r\n";

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string CapaReply =
        "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string RetrReply =
        "+OK 52 octets\r\nSubject: a\r\n\r\nline one\r\n...two dots\r\n..one dot\r\nlast\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string RetrAndQuit = "RETR 1\r\nQUIT\r\n";

    [TestMethod]
    [DataRow("pop3", false)]
    [DataRow("pop3s", true)]
    public async Task CreateRunner_RetrieveMessage_WritesTheUnstuffedMessageAsCurlDoes(string scheme, bool useTls)
    {
        ScriptedConnector connector = new(
            [Encoding.ASCII.GetBytes(Greeting), Encoding.ASCII.GetBytes(CapaReply), Encoding.ASCII.GetBytes(RetrReply), Encoding.ASCII.GetBytes(Bye)]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, $"{scheme}://127.0.0.1:18110/1");

        Diagnostics.AssertWritten(Capa + RetrAndQuit, connector);
        Assert.AreEqual(Capa + RetrAndQuit, Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(("127.0.0.1", 18110, useTls), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        Assert.AreEqual(Message, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    [DataRow("pop3", false)]
    [DataRow("pop3s", true)]
    public async Task CreateRunner_RetrieveMessageWithUser_LogsInWithPlainThroughTheComposedAuthenticator(string scheme, bool useTls)
    {
        ScriptedConnector connector = new(
            [
                Encoding.ASCII.GetBytes(Greeting),
                Encoding.ASCII.GetBytes(CapaReply),
                Encoding.ASCII.GetBytes("+ \r\n"),
                Encoding.ASCII.GetBytes("+OK Authenticated\r\n"),
                Encoding.ASCII.GetBytes(RetrReply),
                Encoding.ASCII.GetBytes(Bye),
            ]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, $"{scheme}://127.0.0.1:18110/1", "-u", "u:p");

        Diagnostics.AssertWritten(Capa + "AUTH PLAIN\r\nAHUAcA==\r\n" + RetrAndQuit, connector);
        Assert.AreEqual(Capa + "AUTH PLAIN\r\nAHUAcA==\r\n" + RetrAndQuit, Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(useTls, connector.Targets.Single().UseTls);
        Assert.AreEqual(Message, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_RetrieveMissingMessage_PrintsWeirdServerReplyAndReturns8()
    {
        ScriptedConnector connector = new(
            [
                Encoding.ASCII.GetBytes(Greeting),
                Encoding.ASCII.GetBytes(CapaReply),
                Encoding.ASCII.GetBytes("-ERR no such message\r\n"),
                Encoding.ASCII.GetBytes(Bye),
            ]);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, "pop3://127.0.0.1:18110/9");

        Diagnostics.AssertWritten(Capa + "RETR 9\r\nQUIT\r\n", connector);
        Assert.AreEqual(Capa + "RETR 9\r\nQUIT\r\n", Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual("curl: (8) Weird server reply" + Environment.NewLine, standardError);
        Assert.AreEqual(8, exitCode);
    }

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        ScriptedConnector connector, string url, params string[] extraArguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        Diagnostics.ArrangeCommandLine(["-sS", .. extraArguments, url]);
        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sS", .. extraArguments, url]);

        (int ExitCode, string StandardOutput, string StandardError) result = (exitCode, Encoding.UTF8.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.ActRun(result.ExitCode, result.StandardOutput, result.StandardError);
        Diagnostics.ActWritten(connector);
        return result;
    }
}
