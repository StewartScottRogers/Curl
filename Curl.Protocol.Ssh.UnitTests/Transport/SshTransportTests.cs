using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshTransportTests
{
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
        SshTransport transport = new(connection, SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0x33));

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        byte[] expectedKexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();
        ScriptedConnection expectedPacket = new();
        await new SshPacketWriter(expectedPacket, new RepeatingRandomSource(0x33)).WriteAsync(expectedKexInit, CancellationToken.None);
        byte[] expectedWritten = [.. Encoding.ASCII.GetBytes("SSH-2.0-libssh2_1.11.1\r\n"), .. expectedPacket.Written];
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
        SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0));

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        Assert.AreEqual("diffie-hellman-group16-sha512", handshake.Algorithms.KeyExchange);
        Assert.AreEqual("rsa-sha2-512", handshake.Algorithms.ServerHostKey);
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
        SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0));

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

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

        await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeBroken);
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

        SshTransferException failure = await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeFailed, SshAlgorithmPreferences.WindowsReference);

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_TodaysCatalogue_FailsEveryServerWithMinus5()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.7").KexInit(SshServerScript.OpenSshKexInit()).Bytes;

        await AssertFailsAsync(serverBytes, SshAlgorithmCatalogue.Implemented, KeyExchangeFailed);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_PeerClosesAfterIdentification_FailsWithMinus1AsMeasured()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Bytes;

        await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeBroken);
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

        await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_TruncatedKexInit_FailsWithMinus1()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Packet(SshMessageNumber.KeyExchangeInit, 1, 2, 3).Bytes;

        await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_DisconnectBeforeKexInit_FailsWithMinus1()
    {
        byte[] serverBytes = new SshServerScript().Line("SSH-2.0-OpenSSH_9.9").Packet(SshMessageNumber.Disconnect, 0, 0, 0, 11).Bytes;

        await AssertFailsAsync(serverBytes, EverythingImplemented, KeyExchangeBroken);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_HttpServer_FailsWithMinus13AsMeasured()
    {
        byte[] serverBytes = new SshServerScript().Line("HTTP/1.1 200 OK").Line("Content-Length: 0").Line(string.Empty).Bytes;
        ScriptedConnection connection = new(serverBytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
        Assert.AreEqual("SSH-2.0-libssh2_1.11.1\r\n", Encoding.ASCII.GetString(connection.Written), "measured: curl sends no KEXINIT without a banner");
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_Cancelled_Throws()
    {
        SshTransport transport = new(new ScriptedConnection(), SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await transport.NegotiateAlgorithmsAsync(new CancellationToken(canceled: true)));
    }

    private static async Task<SshTransferException> AssertFailsAsync(
        byte[] serverBytes,
        SshAlgorithmCatalogue catalogue,
        string expectedMessage,
        SshAlgorithmPreferences? preferences = null)
    {
        SshTransport transport = new(
            new ScriptedConnection(serverBytes),
            preferences ?? SshAlgorithmPreferences.OpenSslReference,
            catalogue,
            new RepeatingRandomSource(0));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(expectedMessage, failure.Message);
        return failure;
    }
}
