using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>imap://</c> and <c>imaps://</c> end to end through the production composition over
/// fake connectors: the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers,
/// the SASL authenticator it composes and the runner's <c>-T</c> handling. Every exchange was
/// recorded from curl 8.21.0 (mingw, Schannel) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Imap</c> (BL-554, BL-555 and BL-557 Notes): curl running
/// <c>-sS [-u u:p] [-T mail.txt] imap://127.0.0.1:18143/INBOX[;UID=1]</c> against the recorder's
/// default replies, whose capabilities offer <c>AUTH=PLAIN</c>, so <c>-u u:p</c> logs in with
/// <c>AUTHENTICATE PLAIN</c>. Each server write is its own read.
/// </summary>
[TestClass]
public sealed class CurlCompositionImapTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready\r\n";

    private const string CapabilityReply = "* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN\r\nA001 OK CAPABILITY completed\r\n";

    private const string Authenticated = "A002 OK Authenticated\r\n";

    /// <summary>The recorder's default message: 100 bytes.</summary>
    private const string Message = "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n";

    private const string Upload = "Subject: hi\r\n\r\nbody\r\n";

    private const string LoggedIn = "A001 CAPABILITY\r\nA002 AUTHENTICATE PLAIN\r\nAHUAcA==\r\n";

    [TestMethod]
    [DataRow("imap", false)]
    [DataRow("imaps", true)]
    public async Task CreateRunner_FetchMessageWithUser_LogsInWithPlainSelectsAndWritesTheMessage(string scheme, bool useTls)
    {
        ScriptedConnector connector = new(
            Reads(
                Greeting,
                CapabilityReply,
                "+ \r\n",
                Authenticated,
                "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n* 2 EXISTS\r\n* 0 RECENT\r\n"
                    + "* OK [UIDVALIDITY 1] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nA003 OK [READ-WRITE] SELECT completed\r\n",
                "* 1 FETCH (UID 1 BODY[] {100}\r\n" + Message + ")\r\nA004 OK FETCH completed\r\n",
                "* BYE Logging out\r\nA005 OK LOGOUT completed\r\n"));

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, ["-u", "u:p", $"{scheme}://127.0.0.1:18143/INBOX;UID=1"]);

        Assert.AreEqual(
            LoggedIn + "A003 SELECT INBOX\r\nA004 UID FETCH 1 BODY[]\r\nA005 LOGOUT\r\n",
            Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(("127.0.0.1", 18143, useTls), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        Assert.AreEqual(Message, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_FetchWithChangedUidValidity_PrintsCurlsErrorAndReturns78()
    {
        ScriptedConnector connector = new(
            Reads(
                Greeting,
                CapabilityReply,
                "* OK [UIDVALIDITY 1] UIDs valid\r\nA002 OK [READ-WRITE] SELECT completed\r\n",
                "* BYE Logging out\r\nA003 OK LOGOUT completed\r\n"));

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, ["imap://127.0.0.1:18143/INBOX;UIDVALIDITY=2;UID=1"]);

        Assert.AreEqual("A001 CAPABILITY\r\nA002 SELECT INBOX\r\nA003 LOGOUT\r\n", Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual("curl: (78) Mailbox UIDVALIDITY has changed" + Environment.NewLine, standardError);
        Diagnostics.Assert("exit code", 78, exitCode);
        Assert.AreEqual(78, exitCode);
    }

    [TestMethod]
    [DataRow("imap", false)]
    [DataRow("imaps", true)]
    public async Task CreateRunner_UploadWithUser_AppendsTheFileWithTheSeenFlag(string scheme, bool useTls)
    {
        ScriptedConnector connector = new(
            Reads(
                Greeting,
                CapabilityReply,
                "+ \r\n",
                Authenticated,
                "+ Ready for literal data\r\n",
                "A003 OK APPEND completed\r\n",
                "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n"));

        (int exitCode, string standardOutput, string standardError) = await RunUploadAsync(
            connector, ["-u", "u:p", "-w", "%{size_upload} %{exitcode}"], $"{scheme}://127.0.0.1:18143/INBOX");

        Assert.AreEqual(
            LoggedIn + "A003 APPEND INBOX (\\Seen) {21}\r\n" + Upload + "\r\nA004 LOGOUT\r\n",
            Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(useTls, connector.Targets.Single().UseTls);
        Assert.AreEqual("21 0", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_UploadRefused_PrintsUploadFailedAndReturns25()
    {
        ScriptedConnector connector = new(
            Reads(
                Greeting,
                CapabilityReply,
                "+ \r\n",
                Authenticated,
                "+ Ready for literal data\r\n",
                "A003 NO [TRYCREATE] no such mailbox\r\n",
                "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n"));

        (int exitCode, string standardOutput, string standardError) = await RunUploadAsync(
            connector, ["-u", "u:p", "-w", "%{size_upload} %{exitcode}"], "imap://127.0.0.1:18143/INBOX");

        Assert.AreEqual(
            LoggedIn + "A003 APPEND INBOX (\\Seen) {21}\r\n" + Upload + "\r\nA004 LOGOUT\r\n",
            Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual("21 25", standardOutput);
        Assert.AreEqual("curl: (25) Upload failed (at start/before it took off)" + Environment.NewLine, standardError);
        Diagnostics.Assert("exit code", 25, exitCode);
        Assert.AreEqual(25, exitCode);
    }

    private static byte[][] Reads(params string[] writes) => [.. writes.Select(Encoding.ASCII.GetBytes)];

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        ScriptedConnector connector, string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        Diagnostics.ArrangeCommandLine(["-sS", .. arguments.Select(argument => Path.IsPathRooted(argument) ? Path.GetFileName(argument) : argument)]);
        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sS", .. arguments]);

        (int ExitCode, string StandardOutput, string StandardError) result = (exitCode, Encoding.UTF8.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.ActRun(result.ExitCode, result.StandardOutput, result.StandardError);
        Diagnostics.ActWritten(connector);
        return result;
    }

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunUploadAsync(
        ScriptedConnector connector, string[] extraArguments, string url)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl558-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string mail = Path.Combine(directory, "mail.txt");
        await System.IO.File.WriteAllBytesAsync(mail, Encoding.ASCII.GetBytes(Upload));

        try
        {
            return await RunAsync(connector, [.. extraArguments, "-T", mail, url]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
