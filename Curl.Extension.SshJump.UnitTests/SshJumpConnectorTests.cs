using Curl.Conformance.SshServer;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Ssh;
using Curl.Ssh.KeyExchange;
using static Curl.Extension.SshJump.InMemoryJumpHost;

namespace Curl.Extension.SshJump;

/// <summary>
/// Pins <see cref="SshJumpConnector" /> against the conformance SSH server in memory, by the sections of
/// RFC 4254 each test names: 7.2 (the <c>direct-tcpip</c> open), 5.1 (its answer), 5.2 (data) and 5.3
/// (EOF and close). The known-hosts file lives in a temporary directory each test makes and deletes.
/// </summary>
[TestClass]
public sealed class SshJumpConnectorTests
{
    private static readonly SshJumpHost JumpHost = new(SshServerClientAccount.User, SshServerClientAccount.Password, "jump.example", 22);

    private static readonly ConnectTarget PlainTarget = new("target.example", 80, UseTls: false);

    [TestMethod]
    public async Task ConnectAsync_Rfc4254Section7_2_ReachesTheJumpHostThenOpensDirectTcpipToTheTarget()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new(ConfirmThenCloseAsync);

        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);
        await result.Connection!.DisposeAsync();
        await jump.Session!;

        ConnectTarget reached = jump.Targets.Single();
        Assert.AreEqual(("jump.example", 22, false), (reached.Host, reached.Port, reached.UseTls));
        CollectionAssert.AreEqual(
            Join([90], String("direct-tcpip"), UInt32(0), UInt32(2097152), UInt32(32768), String("target.example"), UInt32(80), String("127.0.0.1"), UInt32(65535)),
            jump.ChannelOpen);
        Assert.IsTrue(result.Connection.IsSecure);
        Assert.AreEqual("target.example:80", result.Connection.RemoteEndPoint!.ToString()!.Split('/')[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_Rfc4254Sections5_2And5_3_CarriesBytesBothWaysThenClosesTheChannelAndTheJumpConnection()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        byte[]? received = null;
        InMemoryJumpHost jump = new(async (transport, _) =>
        {
            await transport.WritePacketAsync(Confirmation(), CancellationToken.None);
            await transport.WritePacketAsync(Join([94], UInt32(0), String("HTTP/1.1 200 OK"u8.ToArray())), CancellationToken.None);
            received = await transport.ReadPacketAsync(CancellationToken.None);
            CollectionAssert.AreEqual(Join([96], UInt32(ServerChannel)), await transport.ReadPacketAsync(CancellationToken.None));
            await transport.WritePacketAsync(Join([97], UInt32(0)), CancellationToken.None);
            CollectionAssert.AreEqual(Join([97], UInt32(ServerChannel)), await transport.ReadPacketAsync(CancellationToken.None));
        });
        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);
        IConnection connection = result.Connection!;
        byte[] buffer = new byte[64];

        await connection.WriteAsync("GET / HTTP/1.1\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        int read = await connection.ReadAsync(buffer, CancellationToken.None);
        await connection.DisposeAsync();
        await jump.Session!;

        Assert.AreEqual("HTTP/1.1 200 OK", System.Text.Encoding.ASCII.GetString(buffer, 0, read));
        CollectionAssert.AreEqual(Join([94], UInt32(ServerChannel), String("GET / HTTP/1.1\r\n\r\n"u8.ToArray())), received);
    }

    [TestMethod]
    public async Task DisposeAsync_JumpHostAlreadyGone_ClosesWithoutFailing()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new((transport, _) => transport.WritePacketAsync(Confirmation(), CancellationToken.None).AsTask());
        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);
        await jump.Session!;

        await result.Connection!.DisposeAsync();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_Rfc4254Section5_1_JumpHostRefusesTheChannel_FailsWithExit7AndTheReasonCode()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new((transport, _) => transport.WritePacketAsync(Failure(2), CancellationToken.None).AsTask());

        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("SSH jump host curltest@jump.example:22: the jump host refused to connect to target.example port 80 (RFC 4254 reason code 2)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_JumpHostKeyNotInKnownHosts_FailsWithExit60BeforeAuthenticating()
    {
        using KnownHosts home = KnownHosts.Trusting("another.example");
        InMemoryJumpHost jump = new((_, _) => Task.CompletedTask);

        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsNull(jump.ChannelOpen);
    }

    [TestMethod]
    public async Task ConnectAsync_JumpHostClosesBeforeItsIdentification_FailsWithTheHandshakesExit2()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        ClosedConnector closed = new(ConnectResult.Connected(new ClosedConnection()));

        ConnectResult result = await Connector(closed, home).ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.StartsWith("SSH jump host curltest@jump.example:22: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_JumpHostClosesInsteadOfAnsweringTheChannelOpen_FailsWithExit7()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new((_, _) => Task.CompletedTask);

        ConnectResult result = await Connector(jump, home).ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.StartsWith("SSH jump host curltest@jump.example:22: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_CurlsConnectorFails_GivesItsFailureBack()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        ConnectResult failure = ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: jump.example");

        ConnectResult result = await Connector(new ClosedConnector(failure), home).ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreSame(failure, result);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetWithoutCurlsTlsProvider_IsNotBuiltIn()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);

        ConnectResult result = await Connector(new InMemoryJumpHost((_, _) => Task.CompletedTask), home)
            .ConnectAsync(new ConnectTarget("target.example", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual("TLS through the SSH jump host curltest@jump.example:22 is not built in", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsTarget_RunsCurlsTlsOverTheChannelOfferingCurlsAlpnList()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new(ConfirmThenCloseAsync) { HttpOverTlsApplicationProtocols = ["h2", "http/1.1"] };
        RecordingTlsProvider tls = new();

        ConnectResult result = await new SshJumpConnector(JumpHost, jump, home.Services, tls)
            .ConnectAsync(new ConnectTarget("target.example", 443, UseTls: true) { PoolScheme = "https", TlsServerName = "name.example" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();
        await jump.Session!;

        Assert.AreEqual("name.example", tls.TargetHost);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tls.ApplicationProtocols!.ToArray());
        Assert.IsInstanceOfType<SshJumpConnection>(tls.Plaintext);
    }

    [TestMethod]
    public async Task ConnectAsync_TlsTargetOfAnotherSchemeWithoutAServerName_HandsTheTargetsHostAndNoAlpnToTls()
    {
        using KnownHosts home = KnownHosts.Trusting(JumpHost.Host);
        InMemoryJumpHost jump = new(ConfirmThenCloseAsync);
        RecordingTlsProvider tls = new();

        ConnectResult result = await new SshJumpConnector(JumpHost, jump, home.Services, tls)
            .ConnectAsync(new ConnectTarget("mail.example", 993, UseTls: true) { PoolScheme = "imaps" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();
        await jump.Session!;

        Assert.AreEqual("mail.example", tls.TargetHost);
        Assert.IsEmpty(tls.ApplicationProtocols!);
    }

    [TestMethod]
    public void IsJumpHostFailure_TheSessionsFailuresButNoOther()
    {
        Assert.IsTrue(SshJumpConnector.IsJumpHostFailure(new SshTransferException(CurlExitCode.LoginDenied, "denied")));
        Assert.IsTrue(SshJumpConnector.IsJumpHostFailure(new EndOfStreamException()));
        Assert.IsTrue(SshJumpConnector.IsJumpHostFailure(new InvalidDataException()));
        Assert.IsTrue(SshJumpConnector.IsJumpHostFailure(new SshConnectionLostException(new IOException("reset"))));
        Assert.IsFalse(SshJumpConnector.IsJumpHostFailure(new OperationCanceledException()));
    }

    [TestMethod]
    public void ApplicationProtocolsFor_TargetsOwnListOrNoneForAnotherScheme()
    {
        SshJumpConnector connector = new(JumpHost, new InMemoryJumpHost((_, _) => Task.CompletedTask), SshJumpServices.System);
        string[] h2 = ["h2"];

        Assert.AreSame(h2, connector.ApplicationProtocolsFor(new ConnectTarget("a", 443, UseTls: true) { PoolScheme = "HTTPS", ApplicationProtocols = h2 }));
        Assert.IsEmpty(connector.ApplicationProtocolsFor(new ConnectTarget("a", 993, UseTls: true) { PoolScheme = "imaps" }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connector.ConnectAsync(null!, CancellationToken.None).AsTask().GetAwaiter().GetResult());
    }

    private static Task ConfirmThenCloseAsync(SshServerTransport transport, byte[] open) =>
        ConfirmThenCloseCoreAsync(transport);

    private static async Task ConfirmThenCloseCoreAsync(SshServerTransport transport)
    {
        await transport.WritePacketAsync(Confirmation(), CancellationToken.None);
        await transport.ReadPacketAsync(CancellationToken.None);
        await transport.WritePacketAsync(Join([97], UInt32(0)), CancellationToken.None);
        await transport.ReadPacketAsync(CancellationToken.None);
    }

    private static SshJumpConnector Connector(IConnector curlConnector, KnownHosts home) => new(JumpHost, curlConnector, home.Services);

    private static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>A temporary <c>HOME</c> whose <c>.ssh/known_hosts</c> trusts the conformance server's host key for one host.</summary>
    private sealed class KnownHosts : IDisposable
    {
        private readonly string home = Directory.CreateTempSubdirectory("ssh-jump-").FullName;

        private KnownHosts(string host)
        {
            Directory.CreateDirectory(Path.Combine(home, ".ssh"));
            File.WriteAllText(Path.Combine(home, ".ssh", "known_hosts"), $"{host} ssh-ed25519 {Convert.ToBase64String(SshServerHostKey.Blob)}\n");
            Services = new SshJumpServices(new PhysicalFileSystem(), name => name == "HOME" ? home : null, new SystemSshRandomSource(), new SystemSshEphemeralKeySource(), null);
        }

        internal SshJumpServices Services { get; }

        internal static KnownHosts Trusting(string host) => new(host);

        public void Dispose() => Directory.Delete(home, recursive: true);
    }

    private sealed class ClosedConnector(ConnectResult result) : IConnector
    {
        public IReadOnlyList<string> HttpOverTlsApplicationProtocols => ["http/1.1"];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class ClosedConnection : IConnection
    {
        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingTlsProvider : ITlsProvider
    {
        public string? TargetHost { get; private set; }

        public IConnection? Plaintext { get; private set; }

        public IReadOnlyList<string>? ApplicationProtocols { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            AuthenticateAsClientAsync(plaintext, targetHost, NoTransferEvents.Instance, [], cancellationToken);

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken)
        {
            (Plaintext, TargetHost, ApplicationProtocols) = (plaintext, targetHost, applicationProtocols);
            return ValueTask.FromResult(ConnectResult.Connected(plaintext));
        }
    }
}
