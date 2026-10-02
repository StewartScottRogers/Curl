using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the <c>--trace-config ssh</c> lines for the paths BL-1166 left out - uploads, listings,
/// <c>-Q</c> commands, password and <c>keyboard-interactive</c> logins, a failed agent,
/// host-key fingerprints and failed transfers - in the order curl 8.21.0 (libssh2 1.11.1,
/// WinCNG) wrote them against OpenSSH 10.2 on 2026-10-02 (BL-1204 Notes), without the
/// timing-dependent <c>block=1</c> and <c>pollset</c> lines (ADR-0372).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string DoPhaseStarts = "* [SSH] DO phase starts";

    private const string SftpDoneDone = "* [SSH] [SSH_STOP] -> [SSH_SFTP_CLOSE] | * [SSH] SFTP DONE done | * [SSH] [SSH_SFTP_CLOSE] -> [SSH_STOP] | " + Rested;

    private const string SftpUntilTransInit =
        DoPhaseStarts + " | * [SSH] [SSH_STOP] -> [SSH_SFTP_QUOTE_INIT] | * [SSH] [SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_GETINFO] | "
        + "* [SSH] [SSH_SFTP_GETINFO] -> [SSH_SFTP_TRANS_INIT]";

    [TestMethod]
    public async Task ExecuteAsync_TracedSftpUpload_WritesTheUploadStates()
    {
        TraceSetup setup = KeyLogin();

        string lines = await RunTracedAsync("sftp://files.example/up.txt", setup, upload: "up data!");

        StringAssert.Contains(
            lines,
            SftpUntilTransInit + " | * [SSH] [SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_UPLOAD_INIT] | * [SSH] [SSH_SFTP_UPLOAD_INIT] -> [SSH_STOP] | "
            + $"{Rested} | * [SSH] DO phase is complete");
        StringAssert.Contains(lines, $"* upload completely sent off: 8 bytes | {SftpDoneDone} | * Connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedScpUpload_WritesTheUploadStates()
    {
        TraceSetup setup = KeyLogin();

        string lines = await RunTracedAsync("scp://files.example/up.txt", setup, upload: "up data!");

        StringAssert.Contains(
            lines,
            $"{DoPhaseStarts} | * [SSH] [SSH_STOP] -> [SSH_SCP_TRANS_INIT] | * [SSH] [SSH_SCP_TRANS_INIT] -> [SSH_SCP_UPLOAD_INIT] | "
            + $"* [SSH] [SSH_SCP_UPLOAD_INIT] -> [SSH_STOP] | {Rested} | * [SSH] DO phase is complete");
        StringAssert.Contains(
            lines,
            "* upload completely sent off: 8 bytes | * [SSH] [SSH_STOP] -> [SSH_SCP_DONE] | * [SSH] [SSH_SCP_DONE] -> [SSH_SCP_SEND_EOF] | "
            + "* [SSH] [SSH_SCP_SEND_EOF] -> [SSH_SCP_WAIT_EOF] | * [SSH] [SSH_SCP_WAIT_EOF] -> [SSH_SCP_WAIT_CLOSE] | "
            + "* [SSH] [SSH_SCP_WAIT_CLOSE] -> [SSH_SCP_CHANNEL_FREE] | * [SSH] SCP DONE phase complete | "
            + $"* [SSH] [SSH_SCP_CHANNEL_FREE] -> [SSH_STOP] | {Rested} | * Connection #0");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "long names")]
    [DataRow(true, DisplayName = "-l")]
    public async Task ExecuteAsync_TracedSftpListing_WritesTheReaddirStates(bool listOnly)
    {
        TraceSetup setup = KeyLogin();
        setup.Server.Files["/d/a"] = Hello;
        setup.Server.Files["/d/b"] = Hello;
        string perEntry = "* [SSH] [SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_BOTTOM] | * [SSH] [SSH_SFTP_READDIR_BOTTOM] -> [SSH_SFTP_READDIR] | ";

        string lines = await RunTracedAsync("sftp://files.example/d/", setup, listOnly: listOnly);

        // Each entry's data line comes before its READDIR_BOTTOM, as curl's does; left out here.
        string traced = string.Join(" | ", lines.Split(" | ").Where(line => !line.StartsWith("<= ", StringComparison.Ordinal)));
        StringAssert.Contains(
            traced,
            SftpUntilTransInit + " | * [SSH] [SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_READDIR_INIT] | * [SSH] [SSH_SFTP_READDIR_INIT] -> [SSH_SFTP_READDIR] | "
            + (listOnly ? string.Empty : perEntry + perEntry)
            + $"* [SSH] [SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_DONE] | * [SSH] [SSH_SFTP_READDIR_DONE] -> [SSH_STOP] | {Rested} | "
            + $"* [SSH] DO phase is complete | {SftpDoneDone} | * Connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedQuoteCommandsBeforeAndAfter_WritesTheQuoteStates()
    {
        TraceSetup setup = KeyLogin();

        string lines = await RunTracedAsync("sftp://files.example/f", setup, quotes: ["*mkdir /x", "pwd", "-chmod 644 /f"]);

        StringAssert.Contains(
            lines,
            "* [SSH] [SSH_STOP] -> [SSH_SFTP_QUOTE_INIT] | * SSH: sending quote commands | * [SSH] [SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_QUOTE] | "
            + "* [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_QUOTE_MKDIR] | * [SSH] [SSH_SFTP_QUOTE_MKDIR] -> [SSH_SFTP_NEXT_QUOTE] | "
            + "* [SSH] [SSH_SFTP_NEXT_QUOTE] -> [SSH_SFTP_QUOTE] | * [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_NEXT_QUOTE] | "
            + "* [SSH] [SSH_SFTP_NEXT_QUOTE] -> [SSH_SFTP_GETINFO]");
        StringAssert.Contains(
            lines,
            "* [SSH] [SSH_STOP] -> [SSH_SFTP_CLOSE] | * [SSH] SFTP DONE done | * [SSH] [SSH_SFTP_CLOSE] -> [SSH_SFTP_POSTQUOTE_INIT] | "
            + "* SSH: sending quote commands | * [SSH] [SSH_SFTP_POSTQUOTE_INIT] -> [SSH_SFTP_QUOTE] | "
            + "* [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_QUOTE_STAT] | * [SSH] [SSH_SFTP_QUOTE_STAT] -> [SSH_SFTP_QUOTE_SETSTAT] | "
            + "* [SSH] [SSH_SFTP_QUOTE_SETSTAT] -> [SSH_SFTP_NEXT_QUOTE] | * [SSH] [SSH_SFTP_NEXT_QUOTE] -> [SSH_SFTP_CLOSE] | "
            + $"* [SSH] SFTP DONE done | * [SSH] [SSH_SFTP_CLOSE] -> [SSH_STOP] | {Rested} | * Connection #0");
    }

    [TestMethod]
    [DataRow("rename /f /g", "SSH_SFTP_QUOTE_RENAME")]
    [DataRow("rmdir /x", "SSH_SFTP_QUOTE_RMDIR")]
    [DataRow("rm /x", "SSH_SFTP_QUOTE_UNLINK")]
    [DataRow("symlink /f /g", "SSH_SFTP_QUOTE_SYMLINK")]
    [DataRow("*statvfs /", "SSH_SFTP_QUOTE_STATVFS")]
    public async Task ExecuteAsync_TracedQuoteCommand_EntersItsOwnState(string quote, string state)
    {
        string lines = await RunTracedAsync("sftp://files.example/f", KeyLogin(), quotes: [quote]);

        StringAssert.Contains(lines, $"* [SSH] [SSH_SFTP_QUOTE] -> [{state}] | * [SSH] [{state}] -> [SSH_SFTP_NEXT_QUOTE]");
    }

    [TestMethod]
    [DataRow("-rm /gone", "* [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_QUOTE_UNLINK] | * rm \"/gone\" failed: No such file or directory | * [SSH] [SSH_SFTP_QUOTE_UNLINK]", DisplayName = "after")]
    [DataRow("rm /gone", "* [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_QUOTE_UNLINK] | * rm \"/gone\" failed: No such file or directory | * [SSH] [SSH_SFTP_QUOTE_UNLINK]", DisplayName = "before")]
    [DataRow("bogus x", "* [SSH] [SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_QUOTE] | * Unknown SFTP command | * [SSH] [SSH_SFTP_QUOTE]", DisplayName = "unknown")]
    [DataRow("chmod abc /f", "* [SSH] [SSH_SFTP_QUOTE] -> [SSH_SFTP_QUOTE_STAT] | * Syntax error: chmod permissions not a number | * [SSH] [SSH_SFTP_QUOTE_STAT]", DisplayName = "chmod")]
    public async Task ExecuteAsync_TracedQuoteCommandFails_LeavesItsStateForTheClose(string quote, string failedIn)
    {
        TraceSetup setup = KeyLogin();
        setup.Server.RefusedPaths.Add("/gone");

        string lines = await RunTracedAsync("sftp://files.example/f", setup, quotes: [quote]);

        StringAssert.Contains(
            lines,
            $"{failedIn} -> [SSH_SFTP_CLOSE] | * [SSH] [SSH_SFTP_CLOSE] statemachine() -> 21, block=0 | * Connection #0 to host files.example:22 left intact");
        Assert.DoesNotContain("SFTP DONE done | * [SSH] [SSH_SFTP_CLOSE] -> [SSH_STOP]", lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedPasswordLogin_WritesThePasswordStates()
    {
        TraceSetup setup = new(ServerWithAFile(), new NetworkCredential(User, Password), new SshOptions(), []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(
            lines,
            "* [SSH] [SSH_AUTH_PKEY] -> [SSH_AUTH_PASS_INIT] | * [SSH] [SSH_AUTH_PASS_INIT] -> [SSH_AUTH_PASS] | "
            + "* SSH: initialized password authentication | * [SSH] [SSH_AUTH_PASS] -> [SSH_AUTH_DONE] | * SSH: authentication complete");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedLoginFailsEveryMethod_WritesTheAgentStatesAndTheSessionFree()
    {
        TraceSetup setup = new(ServerWithAFile(), new NetworkCredential(User, "wrong"), new SshOptions(), []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(
            lines,
            "* [SSH] [SSH_AUTH_PASS_INIT] -> [SSH_AUTH_PASS] | * [SSH] [SSH_AUTH_PASS] -> [SSH_AUTH_HOST_INIT] | "
            + "* [SSH] [SSH_AUTH_HOST_INIT] -> [SSH_AUTH_AGENT_INIT] | * SSH: trying publickey authentication via agent | "
            + "* SSH: failure connecting to agent | * [SSH] [SSH_AUTH_AGENT_INIT] -> [SSH_AUTH_KEY_INIT] | "
            + "* [SSH] [SSH_AUTH_KEY_INIT] -> [SSH_AUTH_DONE] | * Authentication failure | * [SSH] [SSH_AUTH_DONE] -> [SSH_SESSION_FREE] | "
            + "* [SSH] [SSH_SESSION_FREE] statemachine() -> 67, block=0 | * closing connection #0");
    }

    [TestMethod]
    [DataRow(Password, "* SSH: initialized keyboard interactive authentication | * [SSH] [SSH_AUTH_KEY] -> [SSH_AUTH_DONE] | * SSH: authentication complete", DisplayName = "accepted")]
    [DataRow("wrong", "* [SSH] [SSH_AUTH_KEY] statemachine() -> 67, block=0 | * closing connection #0", DisplayName = "denied")]
    public async Task ExecuteAsync_TracedKeyboardInteractiveLogin_WritesTheKeyStates(string password, string outcome)
    {
        InMemorySshServer server = new(User, Password) { OffersKeyboardInteractive = true };
        server.Files["/f"] = Hello;
        TraceSetup setup = new(server, new NetworkCredential(User, password), new SshOptions(), []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(lines, "* [SSH] [SSH_AUTH_PASS_INIT] -> [SSH_AUTH_HOST_INIT]");
        StringAssert.Contains(lines, "* [SSH] [SSH_AUTH_KEY_INIT] -> [SSH_AUTH_KEY] | " + outcome);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedWithAMatchingSha256Fingerprint_GoesOnToTheAuthList()
    {
        InMemorySshServer server = ServerWithAFile();
        string sha256 = server.HostKeySha256;
        TraceSetup setup = new(server, new NetworkCredential(User, Password), new SshOptions { HostPublicKeySha256 = sha256 }, []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(lines, "* SSH: SHA256 checksum match | * [SSH] [SSH_HOSTKEY] -> [SSH_AUTHLIST]");
    }

    [TestMethod]
    [DataRow(true, DisplayName = "SHA256")]
    [DataRow(false, DisplayName = "MD5")]
    public async Task ExecuteAsync_TracedWithAMismatchedFingerprint_FreesTheSession(bool sha256)
    {
        SshOptions options = sha256 ? new SshOptions { HostPublicKeySha256 = "AAAA" } : new SshOptions { HostPublicKeyMd5 = new string('0', 32) };
        TraceSetup setup = new(ServerWithAFile(), new NetworkCredential(User, Password), options, []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(lines, "* [SSH] [SSH_HOSTKEY] -> [SSH_SESSION_FREE] | * [SSH] [SSH_SESSION_FREE] statemachine() -> 60, block=0 | * closing connection #0");
    }

    [TestMethod]
    [DataRow("sftp://files.example/gone", null, "* [SSH] [SSH_SFTP_DOWNLOAD_INIT] -> [SSH_SFTP_CLOSE] | * [SSH] [SSH_SFTP_CLOSE] statemachine() -> 78", DisplayName = "sftp download")]
    [DataRow("sftp://files.example/gone/", null, "* [SSH] [SSH_SFTP_READDIR_INIT] -> [SSH_SFTP_CLOSE] | * [SSH] [SSH_SFTP_CLOSE] statemachine() -> 78", DisplayName = "sftp listing")]
    [DataRow("scp://files.example/gone", null, "* [SSH] [SSH_SCP_DOWNLOAD_INIT] -> [SSH_SCP_CHANNEL_FREE] | * [SSH] [SSH_SCP_CHANNEL_FREE] statemachine() -> 78", DisplayName = "scp download")]
    [DataRow("sftp://files.example/gone", "up data!", "* [SSH] [SSH_SFTP_UPLOAD_INIT] -> [SSH_SFTP_CLOSE] | * Upload failed: No such file or directory (2/-31) | * [SSH] [SSH_SFTP_CLOSE] statemachine() -> 78", DisplayName = "sftp upload")]
    public async Task ExecuteAsync_TracedTransferFails_LeavesItsStateForTheClose(string url, string? upload, string expected)
    {
        TraceSetup setup = KeyLogin();
        setup.Server.RefusedPaths.Add("/gone");

        string lines = await RunTracedAsync(url, setup, upload: upload);

        StringAssert.Contains(lines, expected + ", block=0 | * Connection #0 to host files.example:22 left intact");
    }

    [TestMethod]
    public void Fail_WhileResting_WritesNothing()
    {
        TranscriptTransferEvents events = new();
        SshStateTrace trace = new(events, enabled: true);

        trace.Fail(CurlExitCode.PartialFile);

        Assert.IsEmpty(events.Transcript);
    }

    private static async Task<string> RunTracedAsync(string url, TraceSetup setup, string? upload = null, IReadOnlyList<string>? quotes = null, bool listOnly = false, bool noBody = false)
    {
        TranscriptTransferEvents events = new();
        TransferContext context = TracedContext(url, setup, events);
        context = new TransferContext
        {
            Url = context.Url,
            Output = context.Output,
            Credentials = context.Credentials,
            Ssh = context.Ssh,
            Events = events,
            Upload = upload is null ? null : new MemoryStream(System.Text.Encoding.ASCII.GetBytes(upload)),
            QuoteCommands = quotes ?? [],
            ListOnly = listOnly,
            NoBody = noBody,
        };
        await HandlerFor(setup, tracesStateMachine: true).ExecuteAsync(context);
        await setup.Server.WhenSessionsEndAsync();
        return string.Join(" | ", events.Transcript);
    }
}
