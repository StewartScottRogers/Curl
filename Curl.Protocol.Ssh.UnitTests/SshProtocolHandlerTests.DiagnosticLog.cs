using System.Net;
using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins what an SCP or SFTP session writes to Curl's own diagnostic log, component
/// <c>ssh</c> (ADR-0222, BL-925): the failure that ends it as <c>error</c>, a refused
/// method and an unchecked host key as <c>warning</c>, the server, algorithms, host key,
/// authenticated method and transfer as <c>info</c>, each message number and SFTP request
/// as <c>verbose</c>, and never a password, pass phrase or private key byte.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string LoggedSecret = "s3cret";

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadAtInfo_LogsTheServerAlgorithmsHostKeyMethodAndTransfer()
    {
        InMemorySshServer server = ServerWithAFile();
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunLoggingAsync(
            server,
            "sftp://files.example/f",
            log,
            new SshOptions { KnownHostsPath = "known_hosts" },
            new Dictionary<string, string> { ["known_hosts"] = server.KnownHostsLine(Host) });

        AssertCheckedOutcomeDiagnostic();
        Assert.AreEqual(TransferResult.Success(Hello.Length), result);
        string[] info = log.At(DiagnosticLogLevel.Info);
        Assert.AreEqual("server identification: " + InMemorySshServer.Identification, info[0]);
        StringAssert.Matches(info[1], new Regex("^negotiated kex [^,]+, host key [^,]+, cipher [^/]+/[^,]+, MAC [^/]+/[^,]+, compression none/none$"));
        StringAssert.Matches(info[2], new Regex("^key exchange [^ ]+ done in [0-9]+ ms$"));
        StringAssert.Matches(info[3], new Regex($"^host key [^ ]+ SHA256:{Regex.Escape(server.HostKeySha256)}$"));
        Assert.AreEqual("host key accepted: it matches known_hosts", info[4]);
        Assert.AreEqual("server allows authentication: publickey,password", info[5]);
        Assert.AreEqual("authenticated with password", info[6]);
        Assert.AreEqual("sftp download of /f started", info[7]);
        StringAssert.Matches(info[8], new Regex("^transfer finished: 11 bytes in [0-9]+ ms$"));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Ssh));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoKnownHostsFile_WarnsTheHostKeyWasTakenUnchecked()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        await RunLoggingAsync(ServerWithAFile(), "sftp://files.example/f", log);

        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Warning), "host key accepted unchecked: no known_hosts file (--insecure)");
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_FingerprintGiven_LogsTheFingerprintVerdict()
    {
        InMemorySshServer server = ServerWithAFile();
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunLoggingAsync(server, "sftp://files.example/f", log, new SshOptions { HostPublicKeySha256 = server.HostKeySha256 });

        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "host key accepted: it matches the fingerprint given");
    }

    [TestMethod]
    public async Task ExecuteAsync_PasswordRefusedThenTheAgentsKey_WarnsThePasswordThenLogsTheAgentAtInfo()
    {
        byte[] publicKey = Convert.FromBase64String(TestUserKeys.RsaPublicKeyFile.Split(' ')[1]);
        InMemorySshServer server = new(User, Password) { AuthorizedPublicKey = publicKey };
        server.Files["/f"] = Hello;
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunLoggingAsync(server, "sftp://files.example/f", log, credentials: new NetworkCredential(User, "wrong"), agent: new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1"));

        string[] messages = [.. log.Lines.Select(line => $"{line.Level} {line.Message}")];
        AssertCheckedOutcomeDiagnostic();
        int refused = Array.IndexOf(messages, "Warning password did not authenticate the user");
        int accepted = Array.IndexOf(messages, "Info authenticated with publickey (ssh-agent)");
        Assert.IsGreaterThanOrEqualTo(0, refused);
        Assert.IsGreaterThan(refused, accepted);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Warning), "publickey did not authenticate the user");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpFileMissing_LogsTheFailureAtErrorWithItsExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunLoggingAsync(ServerWithAFile(), "sftp://files.example/missing.txt", log);

        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.AreEqual(
            new[] { "failed with RemoteFileNotFound (78): Could not open remote file for reading: No such file or directory" },
            log.At(DiagnosticLogLevel.Error));
        Assert.HasCount(1, log.Lines, "at error no warning, info or verbose line is recorded");
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsRefusesTheKey_LogsTheFailureAtErrorBeforeTheTransfer()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunLoggingAsync(ServerWithAFile(), "scp://files.example/f", log, new SshOptions { KnownHostsPath = "known_hosts" });

        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.AreEqual(
            new[] { "failed with PeerFailedVerification (60): SSL peer certificate or SSH remote key was not OK" },
            log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadAtVerbose_LogsMessageNumbersChannelAndSftpRequests()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunLoggingAsync(ServerWithAFile(), "sftp://files.example/missing.txt", log);

        string[] verbose = log.At(DiagnosticLogLevel.Verbose);
        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.Contains(verbose, "sent SSH message 20");
        CollectionAssert.Contains(verbose, "received SSH message 20");
        CollectionAssert.Contains(verbose, "trying authentication with password");
        CollectionAssert.Contains(verbose, "session channel opened");
        CollectionAssert.Contains(verbose, "channel request subsystem sftp: started");
        CollectionAssert.Contains(verbose, "SFTP request 3 id 1");
        CollectionAssert.Contains(verbose, "SFTP status 2");
        CollectionAssert.Contains(verbose, "SFTP answer 104");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpDownloadAtVerbose_LogsTheExecRequest()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunLoggingAsync(ServerWithAFile(), "scp://files.example/f", log);

        AssertCheckedOutcomeDiagnostic();
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "channel request exec scp -pf '/f': started");
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "scp download of /f started");
    }

    [TestMethod]
    public async Task ExecuteAsync_PasswordAtVerbose_NoMessageContainsThePassword()
    {
        InMemorySshServer server = new(User, LoggedSecret);
        server.Files["/f"] = Hello;
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        TransferResult result = await RunLoggingAsync(server, "sftp://files.example/f", log, credentials: new NetworkCredential(User, LoggedSecret));

        AssertCheckedOutcomeDiagnostic();
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(LoggedSecret, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_EncryptedKeyAtVerbose_NoMessageContainsThePassPhraseOrAPrivateKeyByte()
    {
        byte[] publicKey = Convert.FromBase64String(TestUserKeys.Ed25519PublicKeyFile.Split(' ')[1]);
        InMemorySshServer server = new(User, "other password") { AuthorizedPublicKey = publicKey };
        server.Files["/f"] = Hello;
        string keyFile = TestUserKeys.Ed25519OpenSshEncrypted["aes256-ctr"];
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        TransferResult result = await RunLoggingAsync(
            server,
            "sftp://files.example/f",
            log,
            new SshOptions { PrivateKeyPath = "id_test", PrivateKeyPassphrase = TestUserKeys.Passphrase },
            new Dictionary<string, string> { ["id_test"] = keyFile },
            new NetworkCredential(User, string.Empty));

        AssertCheckedOutcomeDiagnostic();
        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "authenticated with publickey");
        string[] keyLines = [.. keyFile.Split('\n').Select(line => line.Trim()).Where(line => line.Length >= 16 && !line.StartsWith("-----", StringComparison.Ordinal))];
        Assert.IsNotEmpty(keyLines);
        foreach ((DiagnosticLogLevel _, string _, string message) in log.Lines)
        {
            Assert.DoesNotContain(TestUserKeys.Passphrase, message);
            Assert.IsFalse(keyLines.Any(line => message.Contains(line[..16], StringComparison.Ordinal)), message);
        }
    }

    private async Task<TransferResult> RunLoggingAsync(
        InMemorySshServer server,
        string url,
        RecordingDiagnosticLog log,
        SshOptions? options = null,
        Dictionary<string, string>? files = null,
        NetworkCredential? credentials = null,
        ISshAgentConnector? agent = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = credentials ?? new NetworkCredential(User, Password),
            Ssh = options,
            DiagnosticLog = log,
        };
        ArrangeTransfer(context);

        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await Handler(server, files, agent: agent).ExecuteAsync(context);
        }

        using (Diagnostics.Phase("session end"))
        {
            await server.WhenSessionsEndAsync();
        }

        ActTransfer(result, context, server);
        checkedOutcome = string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Message}"));
        Diagnostics.Act("log lines", checkedOutcome);
        return result;
    }
}
