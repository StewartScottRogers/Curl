using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins what curl 8.21.0 does with each kind of <c>PASS</c> reply, and which exit code a data
/// connection that cannot be reached ends with (BL-662, ADR-0216). Every case was recorded
/// with <c>Record-CurlExchange.ps1 -Ftp -FtpReply</c> and <c>curl -sS -u u:p</c>. curl 8.21.0
/// never ends with exit 11 for a <c>PASS</c> reply (only for a refused <c>ACCT</c>, BL-635)
/// and never with exit 15: the data connection's host goes through the ordinary connect,
/// so its failures are the connector's own.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerPassReplyTests
{
    private const string Url = "ftp://127.0.0.1:47663/f.txt";

    private const string Greeting = "220 Recorder ready\r\n331 Password required\r\n";

    private const string Retrieved =
        "257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n"
        + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string PassSent = "USER anonymous\r\nPASS ftp@example.com\r\n";

    [TestMethod]
    [DataRow("202 Superfluous")]
    [DataRow("231 Other")]
    public async Task ExecuteAsync_PassAnsweredWithAny2xx_LogsInAndDownloads(string reply)
    {
        // curl -sS -u u:p ftp://127.0.0.1:47663/f.txt, PASS answered 202 or 231: exit 0.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + reply + "\r\n" + Retrieved, "x");

        StringAssert.StartsWith(run.Sent, PassSent + "PWD\r\n");
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual("x", run.OutputText);
    }

    [TestMethod]
    [DataRow("150 Prelim", "Access denied: 150")]
    [DataRow("350 Intermediate", "Access denied: 350")]
    [DataRow("530 Login incorrect", "Access denied: 530")]
    [DataRow("332 Need account", "ACCT requested but none available")]
    public async Task ExecuteAsync_PassAnsweredWithAnythingElse_FailsWithExit67AndNoQuit(string reply, string message)
    {
        // curl: (67) Access denied: 530 - and for 332 without --ftp-account,
        // curl: (67) ACCT requested but none available.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + reply + "\r\n");

        Assert.AreEqual(PassSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, message), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassAnswered421_FailsWithExit28TimeoutWasReached()
    {
        // curl: (28) Timeout was reached
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "421 Bye\r\n");

        Assert.AreEqual(PassSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFtpSkipPasvIpWithAnUnroutableAddress_EndsWithTheConnectorsTimeoutNotExit15()
    {
        // curl -sS --no-ftp-skip-pasv-ip, PASV naming 10.255.255.1: curl: (28) Failed to
        // connect to 127.0.0.1:47705 via 10.255.255.1:56902 after 21066 ms: Could not connect to server
        const string message = "Failed to connect to 10.255.255.1:56902 after 21066 ms: Could not connect to server";
        TransferResult result = await RunPasvAsync(
            "227 Entering Passive Mode (10,255,255,1,222,70)",
            skipPasvIp: false,
            ConnectResult.Failed(CurlExitCode.OperationTimedOut, message),
            out QueuedConnector connector);

        Assert.AreEqual(new ConnectTarget("10.255.255.1", 56902, false), connector.Targets[1]);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.OperationTimedOut, "Failed to connect to 127.0.0.1:47663 via 10.255.255.1:56902 after 21066 ms: Could not connect to server"),
            result with { Report = null });
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSkipPasvIpWithAnUnroutableAddress_ConnectsToTheControlHost()
    {
        // curl -sS, PASV naming 10.255.255.1: the address is skipped and the data connection
        // goes to the control host (exit 0 when it answers); here it is refused to end the run.
        TransferResult result = await RunPasvAsync(
            "227 Entering Passive Mode (10,255,255,1,222,70)",
            skipPasvIp: true,
            ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"),
            out QueuedConnector connector);

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 56902, false), connector.Targets[1]);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataHostThatCannotBeResolved_EndsWithTheConnectorsExit6NotExit15()
    {
        // curl 8.21.0 no longer carries "cannot resolve new host": the data host is resolved
        // by the ordinary connect, so a name that fails is exit 6 like any other.
        const string message = "Could not resolve host: ftp.invalid";
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(
            Greeting + "230 Logged in\r\n257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||61744|)\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(control),
            ConnectResult.Failed(CurlExitCode.CouldntResolveHost, message));

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ftp://ftp.invalid/f.txt"), Output = new MemoryStream() });

        Assert.AreEqual(new ConnectTarget("ftp.invalid", 61744, false), connector.Targets[1]);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntResolveHost, message), result with { Report = null });
    }

    private static Task<TransferResult> RunPasvAsync(string pasvReply, bool skipPasvIp, ConnectResult dataResult, out QueuedConnector connector)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(
            Greeting + "230 Logged in\r\n257 \"/\" is current directory\r\n500 no\r\n" + pasvReply + "\r\n"));
        connector = new QueuedConnector(ConnectResult.Connected(control), dataResult);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            FtpSkipPasvIp = skipPasvIp,
        };

        return new FtpProtocolHandler(connector).ExecuteAsync(context).AsTask();
    }
}
