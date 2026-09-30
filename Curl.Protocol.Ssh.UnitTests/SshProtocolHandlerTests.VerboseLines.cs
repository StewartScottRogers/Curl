using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the <c>-v</c> lines and received data <see cref="SshProtocolHandler" /> reports for
/// whole <c>sftp://</c> and <c>scp://</c> transfers against <see cref="InMemorySshServer" />,
/// in the order curl 8.21.0 wrote them against OpenSSH 10.2 on 2026-09-29 (BL-578,
/// ADR-0262). The connector's own <c>Trying</c> and <c>Established</c> lines are not the
/// handler's and do not appear.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string Start = "* SSH: libssh2 cryptography backend: OpenSSL | * SSH: user 'tester'";

    private const string PasswordLogin =
        "* SSH: host offers authentication via: publickey,password | * SSH: trying private key file '' | "
        + "* SSH: publickey authentication denied: Reason unknown (-1) | * SSH: initialized password authentication | "
        + "* SSH: authentication complete";

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadWithAPassword_ReportsCurlsLinesAndTheData()
    {
        InMemorySshServer server = ServerWithAFile();

        string lines = await RunRecordingLinesAsync(server, "sftp://files.example/f");

        Assert.AreEqual(
            $"{Start} | * SSH: no knownhosts file configured | {PasswordLogin} | <= hello world | * Connection #0 to host files.example:22 left intact",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpDownloadWithAKey_ReportsTheKeyAndTheConnectionEstablished()
    {
        byte[] publicKey = Convert.FromBase64String(TestUserKeys.RsaPublicKeyFile.Split(' ')[1]);
        InMemorySshServer server = new(User, Password) { AuthorizedPublicKey = publicKey };
        server.Files["/f"] = Hello;

        string lines = await RunRecordingLinesAsync(
            server,
            "scp://files.example:2222/f",
            new SshOptions { PrivateKeyPath = "id_test" },
            new Dictionary<string, string> { ["id_test"] = TestUserKeys.RsaPkcs1 },
            credentials: new NetworkCredential(User, string.Empty));

        Assert.AreEqual(
            $"{Start} | * SSH: no knownhosts file configured | * SSH: host offers authentication via: publickey,password | "
            + "* SSH: trying private key file 'id_test' | * SSH: authenticated via publickey | * SSH: authentication complete | "
            + "* SSH: connection established | <= hello world | * Connection #0 to host files.example:2222 left intact",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsHasAnotherKey_ReportsTheCheckAndClosesTheConnectionAsMeasured()
    {
        InMemorySshServer server = ServerWithAFile();
        string otherKey = TestUserKeys.RsaPublicKeyFile.Split(' ')[1];

        string lines = await RunRecordingLinesAsync(
            server,
            "sftp://files.example/f",
            new SshOptions { KnownHostsPath = "known_hosts" },
            new Dictionary<string, string> { ["known_hosts"] = $"{Host} ssh-rsa {otherKey}\n" });

        Assert.AreEqual(
            $"{Start} | * SSH: found host 'files.example' in 'known_hosts' | * SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type | "
            + $"* SSH: host check 1, key: {otherKey} | * SSH: knownhost check failed | * closing connection #0",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsNamesTheKey_ReportsTheMatch()
    {
        InMemorySshServer server = ServerWithAFile();

        string lines = await RunRecordingLinesAsync(
            server,
            "sftp://files.example/f",
            new SshOptions { KnownHostsPath = "known_hosts" },
            new Dictionary<string, string> { ["known_hosts"] = server.KnownHostsLine(Host) });

        StringAssert.Contains(
            lines,
            $"* SSH: host check 0, key: {Convert.ToBase64String(server.HostKeyBlob)} | * SSH: knownhost entry matches host key | * SSH: host offers");
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsFileMissing_ReportsItUnreadAndTheHostNotFound()
    {
        string lines = await RunRecordingLinesAsync(ServerWithAFile(), "sftp://files.example/f", new SshOptions { KnownHostsPath = "known_hosts" });

        Assert.AreEqual(
            $"{Start} | * SSH: failed to read known hosts from known_hosts | * SSH: did not find host 'files.example' in 'known_hosts' | "
            + "* SSH: host check 2, key: <none> | * SSH: knownhost check failed | * closing connection #0",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_FingerprintsGiven_ReportsBothComparisons()
    {
        InMemorySshServer server = ServerWithAFile();
        string sha256 = Convert.ToBase64String(SHA256.HashData(server.HostKeyBlob));
        string md5 = Convert.ToHexStringLower(MD5.HashData(server.HostKeyBlob));

        string lines = await RunRecordingLinesAsync(server, "sftp://files.example/f", new SshOptions { HostPublicKeySha256 = server.HostKeySha256, HostPublicKeyMd5 = md5 });

        StringAssert.StartsWith(
            lines,
            $"{Start} | * SSH: SHA256 public key '{server.HostKeySha256}' | * SSH: SHA256 fingerprint '{sha256}' | * SSH: SHA256 checksum match | "
            + $"* SSH: MD5 public key '{md5}' | * SSH: MD5 fingerprint '{md5}' | * SSH: MD5 checksum match | * SSH: host offers");
    }

    [TestMethod]
    public async Task ExecuteAsync_Md5FingerprintDiffers_WritesTheDenialAsALineAndClosesTheConnection()
    {
        InMemorySshServer server = ServerWithAFile();
        string md5 = Convert.ToHexStringLower(MD5.HashData(server.HostKeyBlob));

        string lines = await RunRecordingLinesAsync(server, "sftp://files.example/f", new SshOptions { HostPublicKeyMd5 = "00" });

        Assert.AreEqual(
            $"{Start} | * SSH: MD5 public key '00' | * SSH: MD5 fingerprint '{md5}' | "
            + $"* Denied establishing ssh session: mismatch MD5 fingerprint. Remote {md5} is not equal to 00 | * closing connection #0",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongPassword_ReportsTheAgentThenTheAuthenticationFailureAsMeasured()
    {
        string lines = await RunRecordingLinesAsync(ServerWithAFile(), "sftp://files.example/f", credentials: new NetworkCredential(User, "wrong"));

        StringAssert.EndsWith(
            lines,
            "* SSH: publickey authentication denied: Reason unknown (-1) | * SSH: trying publickey authentication via agent | "
            + "* SSH: failure connecting to agent | * Authentication failure | * closing connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpFileMissing_WritesTheFailureAndLeavesTheConnectionIntactAsMeasured()
    {
        string lines = await RunRecordingLinesAsync(ServerWithAFile(), "sftp://files.example/missing.txt");

        StringAssert.EndsWith(
            lines,
            "* SSH: authentication complete | * Could not open remote file for reading: No such file or directory | "
            + "* Connection #0 to host files.example:22 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpSubsystemFails_WritesTheFailureAndClosesTheConnection()
    {
        string lines = await RunRecordingLinesAsync(new InMemorySshServer(User, Password) { RefusesSessionChannels = true }, "sftp://files.example/f");

        StringAssert.EndsWith(lines, "* SSH: authentication complete | * Failure initializing sftp session: Unable to startup channel | * closing connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpChannelRefused_WritesTheFailureAndLeavesTheConnectionIntact()
    {
        string lines = await RunRecordingLinesAsync(new InMemorySshServer(User, Password) { RefusesSessionChannels = true }, "scp://files.example/f");

        StringAssert.EndsWith(lines, "* SSH: connection established | * Channel open failure (connect failed) | * Connection #0 to host files.example:22 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpServerHangsUpOnTheChannelOpen_LeavesTheSshLayerErrorUnwritten()
    {
        string lines = await RunRecordingLinesAsync(new InMemorySshServer(User, Password) { HangsUpOnChannelOpen = true }, "scp://files.example/f");

        StringAssert.EndsWith(lines, "* SSH: connection established | * Connection #0 to host files.example:22 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_PresetWithoutABackendAndNoCredentials_ReportsNoBackendAndAnEmptyUser()
    {
        InMemorySshServer server = ServerWithAFile();
        TranscriptTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse("sftp://files.example/f"), Output = new MemoryStream(), Events = events };

        await new SshProtocolHandler(server, new InMemoryKeyFileSystem(new Dictionary<string, string>()), SshAlgorithmPreferences.Full, Encoding.UTF8).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        Assert.AreEqual("* SSH: user ''", events.Transcript[0]);
    }

    [TestMethod]
    [DataRow(CurlExitCode.PartialFile, "end of response with 5 bytes missing", "* end of response with 5 bytes missing", DisplayName = "curl's failf writes a short file's message")]
    [DataRow(CurlExitCode.Ssh, "Error in the SSH layer", "", DisplayName = "the SSH layer's error has no message of its own")]
    public void ReportReturnedFailure_WritesTheMessagesCurlsFailfWrites(CurlExitCode exitCode, string message, string expected)
    {
        TranscriptTransferEvents events = new();

        SshProtocolHandler.ReportReturnedFailure(events, TransferResult.Failure(exitCode, message, 5));
        SshProtocolHandler.ReportReturnedFailure(events, TransferResult.Success(5));

        Assert.AreEqual(expected, string.Join(" | ", events.Transcript));
    }

    private static InMemorySshServer ServerWithAFile()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/f"] = Hello;
        return server;
    }

    private static async Task<string> RunRecordingLinesAsync(
        InMemorySshServer server,
        string url,
        SshOptions? options = null,
        Dictionary<string, string>? files = null,
        NetworkCredential? credentials = null)
    {
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = credentials ?? new NetworkCredential(User, Password),
            Ssh = options,
            Events = events,
        };

        await new SshProtocolHandler(server, new InMemoryKeyFileSystem(files ?? []), SshAlgorithmPreferences.OpenSslReference, Encoding.UTF8).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();
        return string.Join(" | ", events.Transcript);
    }
}
