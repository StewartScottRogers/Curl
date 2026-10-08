using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed partial class SshTransportTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string KeyExchangeFailed = "Failure establishing ssh session: -5, Unable to exchange encryption keys";

    private const string KeyExchangeBroken = "Failure establishing ssh session: -1, Unable to exchange encryption keys";

    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["none"]));

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_SendsIdentificationThenKexInit_AndAgreesWithAnOpenSshServer()
    {
        byte[] serverBytes = new SshServerScript()
            .Line("SSH-2.0-OpenSSH_9.7")
            .KexInit(SshServerScript.OpenSshKexInit())
            .Bytes;
        ScriptedConnection connection = ScriptedConnection.InChunks(serverBytes, 7);
        SshTransport transport = new(connection, SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0x33), new SystemSshEphemeralKeySource());
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("preset", "OpenSslReference, random source 0x33, server chunks of 7 bytes");
        diagnostics.Bytes("server identification line then KEXINIT (OpenSSH_9.7)", serverBytes);

        SshNegotiatedHandshake handshake;
        using (diagnostics.Phase("negotiate"))
        {
            handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);
        }

        diagnostics.Act("client identification", handshake.ClientIdentification);
        diagnostics.Act("server identification", handshake.ServerIdentification);
        diagnostics.Act("key exchange", handshake.Algorithms.KeyExchange);
        diagnostics.Act("server host key", handshake.Algorithms.ServerHostKey);
        diagnostics.Act("cipher client to server", handshake.Algorithms.CipherClientToServer);
        diagnostics.Act("mac client to server", handshake.Algorithms.MacClientToServer ?? "(null)");
        diagnostics.Act("compression server to client", handshake.Algorithms.CompressionServerToClient);
        diagnostics.Act("strict key exchange", handshake.Algorithms.IsStrictKeyExchange);
        byte[] expectedKexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();
        ScriptedConnection expectedPacket = new();
        await new SshPacketWriter(expectedPacket, new RepeatingRandomSource(0x33)).WriteAsync(expectedKexInit, CancellationToken.None);
        byte[] expectedWritten = [.. Encoding.ASCII.GetBytes("SSH-2.0-libssh2_1.11.1\r\n"), .. expectedPacket.Written];
        diagnostics.Diff("bytes written to the server", expectedWritten, connection.Written);
        diagnostics.Assert("client identification", "SSH-2.0-libssh2_1.11.1", handshake.ClientIdentification);
        diagnostics.Assert("server identification", "SSH-2.0-OpenSSH_9.7", handshake.ServerIdentification);
        diagnostics.Assert("key exchange", "curve25519-sha256", handshake.Algorithms.KeyExchange);
        diagnostics.Assert("server host key", "ecdsa-sha2-nistp256", handshake.Algorithms.ServerHostKey);
        diagnostics.Assert("cipher client to server", "chacha20-poly1305@openssh.com", handshake.Algorithms.CipherClientToServer);
        diagnostics.Assert("compression server to client", "none", handshake.Algorithms.CompressionServerToClient);
        diagnostics.Assert("strict key exchange", true, handshake.Algorithms.IsStrictKeyExchange);
        diagnostics.Assert("writer sequence number", 1u, transport.PacketWriter.SequenceNumber);
        diagnostics.Assert("reader sequence number", 1u, transport.PacketReader.SequenceNumber);
        CollectionAssert.AreEqual(expectedWritten, connection.Written);
        Assert.AreEqual("SSH-2.0-libssh2_1.11.1", handshake.ClientIdentification);
        Assert.AreEqual("SSH-2.0-OpenSSH_9.7", handshake.ServerIdentification);
        CollectionAssert.AreEqual(expectedKexInit, handshake.ClientKexInitPayload);
        CollectionAssert.AreEqual(SshServerScript.OpenSshKexInit().ToPayload(), handshake.ServerKexInitPayload);
        Assert.AreEqual("curve25519-sha256", handshake.Algorithms.KeyExchange);
        Assert.AreEqual("ecdsa-sha2-nistp256", handshake.Algorithms.ServerHostKey);
        Assert.AreEqual("chacha20-poly1305@openssh.com", handshake.Algorithms.CipherClientToServer);
        Assert.IsNull(handshake.Algorithms.MacClientToServer);
        Assert.AreEqual("none", handshake.Algorithms.CompressionServerToClient);
        Assert.IsTrue(handshake.Algorithms.IsStrictKeyExchange);
        Assert.AreEqual(1u, transport.PacketWriter.SequenceNumber);
        Assert.AreEqual(1u, transport.PacketReader.SequenceNumber);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_WindowsPresetAgainstAnOpenSshServer_PicksItsFirstSharedNames()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.7").KexInit(SshServerScript.OpenSshKexInit()).Bytes;
        SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("preset", "WindowsReference against OpenSSH_9.7 KEXINIT");
        diagnostics.Bytes("server bytes", serverBytes);

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        diagnostics.Act("key exchange", handshake.Algorithms.KeyExchange);
        diagnostics.Act("server host key", handshake.Algorithms.ServerHostKey);
        diagnostics.Assert("key exchange", "diffie-hellman-group16-sha512", handshake.Algorithms.KeyExchange);
        diagnostics.Assert("server host key", "rsa-sha2-512", handshake.Algorithms.ServerHostKey);
        Assert.AreEqual("diffie-hellman-group16-sha512", handshake.Algorithms.KeyExchange);
        Assert.AreEqual("rsa-sha2-512", handshake.Algorithms.ServerHostKey);
    }

    [TestMethod]
    public async Task StartDelayedCompression_BeforeAnyKeyExchange_LeavesPacketsUncompressed()
    {
        ScriptedConnection connection = new([]);
        SshTransport transport = new(connection, SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload", "single byte 0x02, no key exchange yet, random source 0");

        transport.StartDelayedCompression();
        await transport.PacketWriter.WriteAsync(new byte[] { 2 }, CancellationToken.None);

        ScriptedConnection expected = new([]);
        await new SshPacketWriter(expected, new RepeatingRandomSource(0)).WriteAsync(new byte[] { 2 }, CancellationToken.None);
        diagnostics.Bytes("written by the transport", connection.Written);
        diagnostics.Act("written length", connection.Written.Length);
        diagnostics.Diff("packet bytes", expected.Written, connection.Written);
        diagnostics.Assert("uncompressed packet", expected.Written.Length, connection.Written.Length);
        CollectionAssert.AreEqual(expected.Written, connection.Written);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_IgnoreDebugAndUnimplementedBeforeKexInit_AreSkippedWithoutStrictKex()
    {
        SshKexInit server = SshServerScript.OpenSshKexInit(kexInit => kexInit with { KeyExchange = ["curve25519-sha256"] });
        byte[] serverBytes = new SshServerScript()
            .Line("SSH-2.0-OpenSSH_9.7")
            .Packet(SshMessageNumber.Ignore, 0, 0, 0, 0)
            .Packet(SshMessageNumber.Debug, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            .Packet(SshMessageNumber.Unimplemented, 0, 0, 0, 0)
            .KexInit(server)
            .Bytes;
        SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server packets", "identification, Ignore, Debug, Unimplemented, then KEXINIT with only curve25519-sha256 (no strict kex)");
        diagnostics.Bytes("server bytes", serverBytes);

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        diagnostics.Act("strict key exchange", handshake.Algorithms.IsStrictKeyExchange);
        diagnostics.Act("reader sequence number", transport.PacketReader.SequenceNumber);
        diagnostics.Assert("strict key exchange", false, handshake.Algorithms.IsStrictKeyExchange);
        diagnostics.Assert("reader sequence number", 4u, transport.PacketReader.SequenceNumber);
        Assert.IsFalse(handshake.Algorithms.IsStrictKeyExchange);
        Assert.AreEqual(4u, transport.PacketReader.SequenceNumber);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_IgnoreBeforeKexInitUnderStrictKex_FailsTheKeyExchange()
    {
        byte[] serverBytes = new SshServerScript()
            .Line("SSH-2.0-OpenSSH_9.7")
            .Packet(SshMessageNumber.Ignore, 0, 0, 0, 0)
            .KexInit(SshServerScript.OpenSshKexInit())
            .Bytes;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server packets", "identification, Ignore before KEXINIT under strict kex");

        await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    [DataRow("kex", DisplayName = "KexAlgorithms curve25519-sha256 against the Windows preset, measured")]
    [DataRow("cipher", DisplayName = "Ciphers aes256-gcm@openssh.com against the Windows preset, measured")]
    [DataRow("mac", DisplayName = "MACs umac-64@openssh.com, measured")]
    [DataRow("hostkey", DisplayName = "HostKeyAlgorithms ssh-ed25519 against the Windows preset, measured")]
    public async Task NegotiateAlgorithmsAsync_NoSharedAlgorithm_FailsWithExit2AndMinus5AsMeasured(string list)
    {
        SshKexInit server = SshServerScript.OpenSshKexInit(kexInit => list switch
        {
            "kex" => kexInit with { KeyExchange = ["curve25519-sha256", "ext-info-s", "kex-strict-s-v00@openssh.com"] },
            "cipher" => kexInit with { CipherClientToServer = ["aes256-gcm@openssh.com"], CipherServerToClient = ["aes256-gcm@openssh.com"] },
            "mac" => kexInit with { MacClientToServer = ["umac-64@openssh.com"], MacServerToClient = ["umac-64@openssh.com"], CipherClientToServer = ["aes128-ctr"], CipherServerToClient = ["aes128-ctr"] },
            _ => kexInit with { ServerHostKey = ["ssh-ed25519"] },
        });
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.7").KexInit(server).Bytes;

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("list with no shared algorithm", list);

        SshTransferException failure = await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeFailed, SshAlgorithmPreferences.WindowsReference);

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
    }

    [TestMethod]
    [DataRow(false, "chacha20-poly1305@openssh.com", null, DisplayName = "OpenSSL preset")]
    [DataRow(true, "chacha20-poly1305@openssh.com", null, DisplayName = "Windows preset")]
    public async Task NegotiateAlgorithmsAsync_TodaysCatalogue_AgreesACipherAndMacWithAnOpenSshServer(bool windows, string cipher, string? mac)
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.7").KexInit(SshServerScript.OpenSshKexInit()).Bytes;
        SshAlgorithmPreferences preferences = windows ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;
        SshTransport transport = new(new ScriptedConnection(serverBytes), preferences, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("windows preset", windows);
        diagnostics.Arrange("expected cipher", cipher);
        diagnostics.Arrange("expected mac", mac ?? "(null)");
        diagnostics.Bytes("server bytes", serverBytes);

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        diagnostics.Act("cipher client to server", handshake.Algorithms.CipherClientToServer);
        diagnostics.Act("cipher server to client", handshake.Algorithms.CipherServerToClient);
        diagnostics.Act("mac client to server", handshake.Algorithms.MacClientToServer ?? "(null)");
        diagnostics.Act("mac server to client", handshake.Algorithms.MacServerToClient ?? "(null)");
        diagnostics.Assert("cipher client to server", cipher, handshake.Algorithms.CipherClientToServer);
        diagnostics.Assert("cipher server to client", cipher, handshake.Algorithms.CipherServerToClient);
        diagnostics.Assert("mac client to server", mac ?? "(null)", handshake.Algorithms.MacClientToServer ?? "(null)");
        diagnostics.Assert("mac server to client", mac ?? "(null)", handshake.Algorithms.MacServerToClient ?? "(null)");
        Assert.AreEqual(cipher, handshake.Algorithms.CipherClientToServer);
        Assert.AreEqual(cipher, handshake.Algorithms.CipherServerToClient);
        Assert.AreEqual(mac, handshake.Algorithms.MacClientToServer);
        Assert.AreEqual(mac, handshake.Algorithms.MacServerToClient);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_PeerClosesAfterIdentification_FailsWithMinus1AsMeasured()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Bytes;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server", "closes after sending its identification line");

        await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    [DataRow(0x00100000u, (byte)4, DisplayName = "1 MiB packet, measured")]
    [DataRow(12u, (byte)2, DisplayName = "padding under four, measured")]
    public async Task NegotiateAlgorithmsAsync_BrokenFraming_FailsWithMinus1AsMeasured(uint packetLength, byte paddingLength)
    {
        byte[] serverBytes = new SshServerScript()
            .Line("SSH-2.0-OpenSSH_9.9")
            .RawPacket(packetLength, paddingLength, Encoding.ASCII.GetBytes("\u0014" + new string('A', 40)))
            .Bytes;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("packet length", packetLength);
        diagnostics.Arrange("padding length", paddingLength);

        await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_TruncatedKexInit_FailsWithMinus1()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Packet(SshMessageNumber.KeyExchangeInit, 1, 2, 3).Bytes;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server packet", "KeyExchangeInit truncated to bytes 1, 2, 3");

        await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_DisconnectBeforeKexInit_FailsWithMinus1()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Packet(SshMessageNumber.Disconnect, 0, 0, 0, 11).Bytes;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server packet", "Disconnect (reason 11) before KEXINIT");

        await AssertFailsAsync(diagnostics, serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_HttpServer_FailsWithMinus13AsMeasured()
    {
        byte[] serverBytes = new SshServerScript().Line("HTTP/1.1 200 OK").Line("Content-Length: 0").Line(string.Empty).Bytes;
        ScriptedConnection connection = new(serverBytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server", "HTTP/1.1 200 OK response instead of an SSH banner");
        diagnostics.Bytes("server bytes", serverBytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        diagnostics.Act("exception", failure.GetType().Name + ": " + failure.Message);
        diagnostics.Bytes("written to the server", connection.Written);
        diagnostics.Assert("message", "Failure establishing ssh session: -13, Failed getting banner", failure.Message);
        diagnostics.Assert("written", "SSH-2.0-libssh2_1.11.1\r\n", Encoding.ASCII.GetString(connection.Written));
        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
        Assert.AreEqual("SSH-2.0-libssh2_1.11.1\r\n", Encoding.ASCII.GetString(connection.Written), "measured: curl sends no KEXINIT without a banner");
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_PeerResetsTheConnectionAfterTheBanner_FailsWithMinus1AsMeasured()
    {
        ResettingConnection connection = new(false, Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.6\r\n"));
        SshTransport transport = new(connection, SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server", "sends SSH-2.0-OpenSSH_9.6 banner then resets the connection");

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        diagnostics.Act("exception", failure.GetType().Name + ": " + failure.Message);
        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        diagnostics.Assert("message", KeyExchangeBroken, failure.Message);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(KeyExchangeBroken, failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_Cancelled_Throws()
    {
        SshTransport transport = new(new ScriptedConnection(), SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("cancellation token", "already cancelled");

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await transport.NegotiateAlgorithmsAsync(new CancellationToken(canceled: true)));

        diagnostics.Act("exception", cancelled.GetType().Name);
        diagnostics.Assert("exception type", typeof(OperationCanceledException).Name, cancelled.GetType().Name);
    }

    private static async Task<SshTransferException> AssertFailsAsync(
        TestDiagnostics diagnostics,
        byte[] serverBytes,
        SshAlgorithmCatalogue catalogue,
        string expectedMessage,
        SshAlgorithmPreferences? preferences = null)
    {
        SshTransport transport = new(
            new ScriptedConnection(serverBytes),
            preferences ?? SshAlgorithmPreferences.OpenSslReference,
            catalogue,
            new RepeatingRandomSource(0), new SystemSshEphemeralKeySource());
        diagnostics.Arrange("preset", preferences is null ? "OpenSslReference" : "caller-supplied preferences");
        diagnostics.Bytes("scripted server bytes", serverBytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        diagnostics.Act("exception", failure.GetType().Name + ": " + failure.Message);
        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        diagnostics.Assert("message", expectedMessage, failure.Message);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(expectedMessage, failure.Message);
        return failure;
    }
}
