using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the <c>--trace-config ssh</c> lines, <c>[SSH] ...</c>, <see cref="SshProtocolHandler" />
/// writes among its <c>-v</c> lines when <see cref="SshProtocolHandler.TracesStateMachine" /> is
/// on, in the order curl 8.21.0 (libssh2 1.11.1, WinCNG) wrote them for an SFTP and an SCP
/// download from OpenSSH 10.2 on 2026-10-02 (BL-1166 Notes), without the timing-dependent
/// <c>block=1</c> and <c>pollset</c> lines (ADR-0372).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string TracedKeyLogin =
        "* [SSH] [SSH_STOP] -> [SSH_INIT] | * [SSH] [SSH_INIT] -> [SSH_S_STARTUP] | * [SSH] [SSH_S_STARTUP] -> [SSH_HOSTKEY] | "
        + "* [SSH] no host key checksum given, checking knownhosts | * SSH: no knownhosts file configured | "
        + "* [SSH] [SSH_HOSTKEY] -> [SSH_AUTHLIST] | * SSH: host offers authentication via: publickey,password | "
        + "* [SSH] [SSH_AUTHLIST] -> [SSH_AUTH_PKEY_INIT] | * SSH: trying private key file 'id_test' | "
        + "* [SSH] [SSH_AUTH_PKEY_INIT] -> [SSH_AUTH_PKEY] | * SSH: authenticated via publickey | "
        + "* [SSH] [SSH_AUTH_PKEY] -> [SSH_AUTH_DONE] | * SSH: authentication complete";

    private const string Rested = "* [SSH] [SSH_STOP] statemachine() -> 0, block=0";

    [TestMethod]
    public async Task ExecuteAsync_TracedSftpDownloadWithAKey_WritesCurlsSshLines()
    {
        string lines = await RunTracedAsync("sftp://files.example/f", KeyLogin());

        Assert.AreEqual(
            $"{Start} | {TracedKeyLogin} | "
            + "* [SSH] [SSH_AUTH_DONE] -> [SSH_SFTP_INIT] | * [SSH] [SSH_SFTP_INIT] -> [SSH_SFTP_REALPATH] | "
            + $"* [SSH] [SSH_SFTP_REALPATH] -> [SSH_STOP] | * [SSH] CONNECT phase done | {Rested} | * [SSH] DO phase starts | "
            + "* [SSH] [SSH_STOP] -> [SSH_SFTP_QUOTE_INIT] | * [SSH] [SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_GETINFO] | "
            + "* [SSH] [SSH_SFTP_GETINFO] -> [SSH_SFTP_TRANS_INIT] | * [SSH] [SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_DOWNLOAD_INIT] | "
            + "* [SSH] [SSH_SFTP_DOWNLOAD_INIT] -> [SSH_SFTP_DOWNLOAD_STAT] | * [SSH] [SSH_SFTP_DOWNLOAD_STAT] -> [SSH_STOP] | "
            + $"{Rested} | * [SSH] DO phase is complete | <= hello world | * [SSH] [SSH_STOP] -> [SSH_SFTP_CLOSE] | "
            + $"* [SSH] SFTP DONE done | * [SSH] [SSH_SFTP_CLOSE] -> [SSH_STOP] | {Rested} | "
            + "* Connection #0 to host files.example:22 left intact",
            lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedScpDownloadWithAKey_WritesCurlsSshLines()
    {
        string lines = await RunTracedAsync("scp://files.example/f", KeyLogin());

        Assert.AreEqual(
            $"{Start} | {TracedKeyLogin} | * SSH: connection established | * [SSH] [SSH_AUTH_DONE] -> [SSH_STOP] | {Rested} | "
            + "* [SSH] DO phase starts | * [SSH] [SSH_STOP] -> [SSH_SCP_TRANS_INIT] | * [SSH] [SSH_SCP_TRANS_INIT] -> [SSH_SCP_DOWNLOAD_INIT] | "
            + $"* [SSH] [SSH_SCP_DOWNLOAD_INIT] -> [SSH_STOP] | {Rested} | * [SSH] DO phase is complete | <= hello world | "
            + "* [SSH] [SSH_STOP] -> [SSH_SCP_DONE] | * [SSH] [SSH_SCP_DONE] -> [SSH_SCP_CHANNEL_FREE] | * [SSH] SCP DONE phase complete | "
            + $"* [SSH] [SSH_SCP_CHANNEL_FREE] -> [SSH_STOP] | {Rested} | * Connection #0 to host files.example:22 left intact",
            lines);
    }

    [TestMethod]
    [DataRow("sftp://files.example/f")]
    [DataRow("scp://files.example/f")]
    public async Task ExecuteAsync_NotTraced_WritesNoSshLines(string url)
    {
        TraceSetup setup = KeyLogin();
        TranscriptTransferEvents events = new();

        await HandlerFor(setup, tracesStateMachine: false).ExecuteAsync(TracedContext(url, setup, events));
        await setup.Server.WhenSessionsEndAsync();

        Assert.IsFalse(events.Transcript.Any(line => line.Contains("[SSH]", StringComparison.Ordinal)));
        Assert.IsTrue(events.Transcript.Contains("* SSH: authentication complete"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedKeyDenied_GoesOnToThePasswordState()
    {
        TraceSetup setup = new(ServerWithAFile(), new NetworkCredential(User, Password), new SshOptions(), []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(lines, "* [SSH] [SSH_AUTH_PKEY_INIT] -> [SSH_AUTH_PKEY] | " + NoKeyDenied + " | * [SSH] [SSH_AUTH_PKEY] -> [SSH_AUTH_PASS_INIT]");
    }

    [TestMethod]
    public async Task ExecuteAsync_TracedWithAnMd5Fingerprint_WritesNoKnownHostsCheckLine()
    {
        InMemorySshServer server = ServerWithAFile();
        string md5 = Convert.ToHexStringLower(MD5.HashData(server.HostKeyBlob));
        TraceSetup setup = new(server, new NetworkCredential(User, Password), new SshOptions { HostPublicKeyMd5 = md5 }, []);

        string lines = await RunTracedAsync("sftp://files.example/f", setup);

        StringAssert.Contains(lines, $"* [SSH] [SSH_S_STARTUP] -> [SSH_HOSTKEY] | * SSH: MD5 public key '{md5}'");
        Assert.DoesNotContain("checking knownhosts", lines);
    }

    private static TraceSetup KeyLogin()
    {
        byte[] publicKey = Convert.FromBase64String(TestUserKeys.RsaPublicKeyFile.Split(' ')[1]);
        InMemorySshServer server = new(User, Password) { AuthorizedPublicKey = publicKey };
        server.Files["/f"] = Hello;
        return new TraceSetup(
            server,
            new NetworkCredential(User, string.Empty),
            new SshOptions { PrivateKeyPath = "id_test" },
            new Dictionary<string, string> { ["id_test"] = TestUserKeys.RsaPkcs1 });
    }

    private static async Task<string> RunTracedAsync(string url, TraceSetup setup)
    {
        TranscriptTransferEvents events = new();
        await HandlerFor(setup, tracesStateMachine: true).ExecuteAsync(TracedContext(url, setup, events));
        await setup.Server.WhenSessionsEndAsync();
        return string.Join(" | ", events.Transcript);
    }

    private static TransferContext TracedContext(string url, TraceSetup setup, TranscriptTransferEvents events) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Credentials = setup.Credentials,
        Ssh = setup.Options,
        Events = events,
    };

    private static SshProtocolHandler HandlerFor(TraceSetup setup, bool tracesStateMachine) =>
        new(
            setup.Server,
            new InMemoryKeyFileSystem(setup.Files),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent())
        {
            TracesStateMachine = tracesStateMachine,
        };

    // The server, the login and the key files one traced transfer runs with.
    private sealed record TraceSetup(InMemorySshServer Server, NetworkCredential Credentials, SshOptions Options, Dictionary<string, string> Files);
}
