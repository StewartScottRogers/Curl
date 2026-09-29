using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Runs whole <c>sftp://</c> and <c>scp://</c> transfers through
/// <see cref="SshProtocolHandler" /> against <see cref="InMemorySshServer" />, pinning the
/// bytes written and the outcome of the success and failure cases BL-567 to BL-574 measured
/// from curl 8.21.0 (ADR-0213, ADR-0215, ADR-0220, ADR-0225).
/// </summary>
[TestClass]
public sealed class SshProtocolHandlerTests
{
    private const string User = "tester";

    private const string Password = "secret";

    private const string Host = "files.example";

    private static readonly byte[] Hello = "hello world"u8.ToArray();

    public static IEnumerable<object[]> Presets =>
    [
        ["Windows", SshAlgorithmPreferences.WindowsReference],
        ["OpenSSL", SshAlgorithmPreferences.OpenSslReference],
    ];

    [TestMethod]
    public void SupportedSchemes_AreExactlyScpAndSftp()
    {
        SshProtocolHandler handler = Handler(Server());

        CollectionAssert.AreEquivalent(new[] { "scp", "sftp" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Presets))]
    public async Task ExecuteAsync_SftpDownload_WritesTheFileAndEndsTheSessionAsCurlDoes(string platform, SshAlgorithmPreferences preferences)
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = Hello;

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/data/hello.txt", preferences: preferences);

        Assert.AreEqual(TransferResult.Success(Hello.Length), outcome.Result, platform);
        CollectionAssert.AreEqual(Hello, outcome.Output);
        Assert.AreEqual(Host, server.Targets[0].Host);
        Assert.AreEqual(22, server.Targets[0].Port, "both schemes default to port 22");
        Assert.IsFalse(server.Targets[0].UseTls);
        AssertEvents(
            server,
            "service ssh-userauth", $"auth none {User} refused", $"auth password {User} ok", "channel open session", "subsystem sftp",
            "sftp 16 .", "sftp 3 /data/hello.txt", "sftp 17 /data/hello.txt", "sftp 5 /data/hello.txt",
            "sftp 4 /data/hello.txt", "channel eof", "channel close", "disconnect 11 Shutdown");
    }

    [TestMethod]
    [DynamicData(nameof(Presets))]
    public async Task ExecuteAsync_ScpDownload_WritesTheFileAndEndsTheSessionAsCurlDoes(string platform, SshAlgorithmPreferences preferences)
    {
        InMemorySshServer server = Server();
        byte[] large = [.. Enumerable.Range(0, 70000).Select(index => (byte)index)];
        server.Files["/data/large.bin"] = large;

        Outcome outcome = await RunAsync(server, $"scp://{Host}/data/large.bin", preferences: preferences);

        Assert.AreEqual(TransferResult.Success(large.Length), outcome.Result, platform);
        CollectionAssert.AreEqual(large, outcome.Output);
        AssertEvents(
            server,
            "service ssh-userauth", $"auth none {User} refused", $"auth password {User} ok", "channel open session", "exec scp -pf '/data/large.bin'",
            "channel eof", "channel close", "disconnect 11 Shutdown");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpPathUnderTheHomeDirectory_OpensItThere()
    {
        InMemorySshServer server = Server();
        server.Files[InMemorySshServer.DefaultHomeDirectory + "/notes.txt"] = Hello;

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/~/notes.txt");

        Assert.AreEqual(TransferResult.Success(Hello.Length), outcome.Result);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_PortInTheUrl_ConnectsToIt()
    {
        InMemorySshServer server = Server();
        server.Files["/f"] = Hello;

        await RunAsync(server, $"scp://{Host}:2222/f");

        Assert.AreEqual(2222, server.Targets[0].Port);
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpMissingFile_IsExit78AsMeasured()
    {
        InMemorySshServer server = Server();

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/missing.txt");

        AssertFailure(outcome, CurlExitCode.RemoteFileNotFound, "Could not open remote file for reading: No such file or directory");
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown", "the session still ends with DISCONNECT");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpMissingFile_IsExit78AsMeasured()
    {
        InMemorySshServer server = Server();

        Outcome outcome = await RunAsync(server, $"scp://{Host}/f/missing.txt");

        AssertFailure(outcome, CurlExitCode.RemoteFileNotFound, "Failed to recv file");
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown", "ADR-0225: the handler ends the session after a header failure");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScpChannelRefused_IsExit79AsMeasured()
    {
        InMemorySshServer server = new(User, Password) { RefusesSessionChannels = true };

        Outcome outcome = await RunAsync(server, $"scp://{Host}/f");

        AssertFailure(outcome, CurlExitCode.Ssh, "Channel open failure (connect failed)");
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpChannelRefused_IsExit2AsMeasured()
    {
        InMemorySshServer server = new(User, Password) { RefusesSessionChannels = true };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f");

        AssertFailure(outcome, CurlExitCode.FailedInit, "Failure initializing sftp session: Unable to startup channel");
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerHangsUpOnTheChannelOpen_IsExit2AndTheLostDisconnectIsIgnored()
    {
        InMemorySshServer server = new(User, Password) { HangsUpOnChannelOpen = true };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f");

        AssertFailure(outcome, CurlExitCode.FailedInit, "Failure initializing sftp session: Unable to startup channel");
        CollectionAssert.DoesNotContain(server.Events.ToArray(), "disconnect 11 Shutdown");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoKeyExchangeInCommon_IsExit2AsMeasuredAndSendsNoDisconnect()
    {
        InMemorySshServer server = Server();
        SshAlgorithmPreferences curveOnly = SshAlgorithmPreferences.OpenSslReference with { KeyExchange = ["curve25519-sha256"] };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", preferences: curveOnly);

        AssertFailure(outcome, CurlExitCode.FailedInit, "Failure establishing ssh session: -5, Unable to exchange encryption keys");
        CollectionAssert.DoesNotContain(server.Events.ToArray(), "disconnect 11 Shutdown");
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongPassword_IsExit67AsMeasured()
    {
        InMemorySshServer server = Server();
        server.Files["/f"] = Hello;

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", credentials: new NetworkCredential(User, "wrong"));

        AssertFailure(outcome, CurlExitCode.LoginDenied, "Authentication failure");
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown");
    }

    [TestMethod]
    public async Task ExecuteAsync_KeyFileTheServerAuthorizes_AuthenticatesWithPublickey()
    {
        byte[] publicKey = Convert.FromBase64String(TestUserKeys.RsaPublicKeyFile.Split(' ')[1]);
        InMemorySshServer server = new(User, Password) { AuthorizedPublicKey = publicKey };
        server.Files["/f"] = Hello;
        SshOptions options = new() { PrivateKeyPath = "id_test" };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", options, new NetworkCredential(User, string.Empty), files: new() { ["id_test"] = TestUserKeys.RsaPkcs1 });

        Assert.AreEqual(TransferResult.Success(Hello.Length), outcome.Result);
        CollectionAssert.Contains(server.Events.ToArray(), $"auth publickey {User} ok");
    }

    [TestMethod]
    public async Task ExecuteAsync_HostKeyFingerprintDiffers_IsExit60AsMeasured()
    {
        InMemorySshServer server = Server();
        string remote = Convert.ToBase64String(SHA256.HashData(server.HostKeyBlob));
        SshOptions options = new() { HostPublicKeySha256 = "AAAA" };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", options);

        AssertFailure(outcome, CurlExitCode.PeerFailedVerification, $"Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {remote} is not equal to AAAA");
        CollectionAssert.DoesNotContain(server.Events.ToArray(), $"auth password {User} ok", "the host key is refused before the user authenticates");
    }

    [TestMethod]
    public async Task ExecuteAsync_HostKeyFingerprintMatches_Downloads()
    {
        InMemorySshServer server = Server();
        server.Files["/f"] = Hello;
        SshOptions options = new() { HostPublicKeySha256 = server.HostKeySha256 };

        Outcome outcome = await RunAsync(server, $"scp://{Host}/f", options);

        Assert.AreEqual(TransferResult.Success(Hello.Length), outcome.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsNamesTheServersKey_Downloads()
    {
        InMemorySshServer server = Server();
        server.Files["/f"] = Hello;
        SshOptions options = new() { KnownHostsPath = "known_hosts" };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", options, files: new() { ["known_hosts"] = server.KnownHostsLine(Host) });

        Assert.AreEqual(TransferResult.Success(Hello.Length), outcome.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsHasAnotherKeyForTheHost_IsExit60AsMeasured()
    {
        InMemorySshServer server = Server();
        using RSA otherKey = RSA.Create(1024);
        RSAParameters otherPublic = otherKey.ExportParameters(false);
        byte[] otherBlob = SshTestEncoding.Join(SshTestEncoding.Name("ssh-rsa"), SshTestEncoding.Mpint(otherPublic.Exponent!), SshTestEncoding.Mpint(otherPublic.Modulus!));
        SshOptions options = new() { KnownHostsPath = "known_hosts" };

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", options, files: new() { ["known_hosts"] = $"{Host} ssh-rsa {Convert.ToBase64String(otherBlob)}\n" });

        AssertFailure(outcome, CurlExitCode.PeerFailedVerification, "SSL peer certificate or SSH remote key was not OK");
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownHostsEntryOfAnUnknownType_IsExit79BeforeConnecting()
    {
        InMemorySshServer server = Server();
        SshOptions options = new() { KnownHostsPath = "known_hosts" };
        string line = $"{Host} ssh-dss {Convert.ToBase64String(server.HostKeyBlob)}\n";

        Outcome outcome = await RunAsync(server, $"sftp://{Host}/f", options, files: new() { ["known_hosts"] = line });

        AssertFailure(outcome, CurlExitCode.Ssh, "Unknown host key type: 3932160");
        Assert.IsEmpty(server.Targets, "nothing is connected");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsOutcome()
    {
        SshProtocolHandler handler = new(new RefusingConnector(), new InMemoryKeyFileSystem(new Dictionary<string, string>()), SshAlgorithmPreferences.OpenSslReference, Encoding.UTF8);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse($"sftp://{Host}/f"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        SshProtocolHandler handler = Handler(Server());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    [DataRow(0, "connector")]
    [DataRow(1, "fileSystem")]
    [DataRow(2, "preferences")]
    [DataRow(3, "credentialEncoding")]
    public void Constructor_NullArgument_Throws(int missing, string name)
    {
        IConnector? connector = missing == 0 ? null : Server();
        IFileSystem? fileSystem = missing == 1 ? null : new InMemoryKeyFileSystem(new Dictionary<string, string>());
        SshAlgorithmPreferences? preferences = missing == 2 ? null : SshAlgorithmPreferences.OpenSslReference;
        Encoding? encoding = missing == 3 ? null : Encoding.UTF8;

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SshProtocolHandler(connector!, fileSystem!, preferences!, encoding!));

        Assert.AreEqual(name, exception.ParamName);
    }

    private static InMemorySshServer Server() => new(User, Password);

    private static SshProtocolHandler Handler(InMemorySshServer server, Dictionary<string, string>? files = null, SshAlgorithmPreferences? preferences = null) =>
        new(server, new InMemoryKeyFileSystem(files ?? []), preferences ?? SshAlgorithmPreferences.OpenSslReference, Encoding.UTF8);

    private static async Task<Outcome> RunAsync(
        InMemorySshServer server,
        string url,
        SshOptions? options = null,
        NetworkCredential? credentials = null,
        Dictionary<string, string>? files = null,
        SshAlgorithmPreferences? preferences = null)
    {
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = credentials ?? new NetworkCredential(User, Password),
            Ssh = options,
        };

        TransferResult result = await Handler(server, files, preferences).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();
        return new Outcome(result, output.ToArray());
    }

    private static void AssertFailure(Outcome outcome, CurlExitCode exitCode, string message)
    {
        Assert.AreEqual(exitCode, outcome.Result.ExitCode);
        Assert.AreEqual(message, outcome.Result.ErrorMessage);
        Assert.IsEmpty(outcome.Output);
    }

    private static void AssertEvents(InMemorySshServer server, params string[] expected) =>
        Assert.AreEqual(string.Join(" | ", expected), string.Join(" | ", server.Events));

    private sealed record Outcome(TransferResult Result, byte[] Output);

    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused("Failed to connect"));
    }
}
