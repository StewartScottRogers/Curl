using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Connection;

/// <summary>
/// Pins <see cref="SshSessionChannel" /> against an in-memory peer: the channel messages
/// the client sends byte for byte, with the window and packet size measured 2026-09-29 in
/// OpenSSH's log of curl 8.21.0's <c>CHANNEL_OPEN</c> (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed class SshSessionChannelTests
{
    private static readonly byte[] ServerChannelBytes = UInt32(SftpServerScript.ServerChannel);

    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["none"]));

    private static byte[] ClientKexInit =>
        SshKexInit.ForClient(SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();

    [TestMethod]
    public async Task OpenAsync_ServerConfirms_SendsASessionOpenWithLibssh2sWindowAndPacketSizeAsMeasured()
    {
        Peer peer = Connect(new SftpServerScript().Confirm());

        Assert.IsTrue(await peer.Channel.OpenAsync(CancellationToken.None));

        AssertWritten(peer, Join([SshConnectionMessageNumber.ChannelOpen], Name("session"), UInt32(0), UInt32(2097152), UInt32(32768)));
    }

    [TestMethod]
    public async Task OpenAsync_ServerRefuses_ReturnsFalseAndKeepsTheReasonCode()
    {
        Peer peer = Connect(new SftpServerScript().Ssh(Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(0), UInt32(2), Name("refused"), Name(string.Empty))));
        Assert.AreEqual(0u, peer.Channel.OpenFailureReasonCode);

        Assert.IsFalse(await peer.Channel.OpenAsync(CancellationToken.None));

        Assert.AreEqual(2u, peer.Channel.OpenFailureReasonCode);
    }

    [TestMethod]
    public async Task OpenAsync_OtherMessagesFirst_SkipsThemAndRefusesAGlobalRequestThatWantsAReply()
    {
        Peer peer = Connect(new SftpServerScript()
            .Ssh(SshMessageNumber.Ignore, 0, 0, 0, 0)
            .Ssh(Join([SshConnectionMessageNumber.GlobalRequest], Name("keepalive@openssh.com"), [1]))
            .Ssh(Join([SshConnectionMessageNumber.GlobalRequest], Name("hostkeys-00@openssh.com"), [0]))
            .Confirm());

        Assert.IsTrue(await peer.Channel.OpenAsync(CancellationToken.None));

        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(2, written);
        CollectionAssert.AreEqual(new byte[] { SshConnectionMessageNumber.RequestFailure }, written[1]);
    }

    [TestMethod]
    public async Task OpenAsync_ServerDisconnects_ThrowsEndOfStream()
    {
        Peer peer = Connect(new SftpServerScript().Ssh(Join([SshMessageNumber.Disconnect], UInt32(11), Name("bye"), Name(string.Empty))));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await peer.Channel.OpenAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RequestSubsystemAsync_ServerAccepts_SendsTheRequestWithAReplyWanted()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)]));

        Assert.IsTrue(await peer.Channel.RequestSubsystemAsync("sftp", CancellationToken.None));

        CollectionAssert.AreEqual(
            Join([SshConnectionMessageNumber.ChannelRequest], ServerChannelBytes, Name("subsystem"), [1], Name("sftp")),
            SftpServerScript.SshPayloads(peer.Connection.Written)[1]);
    }

    [TestMethod]
    public async Task RequestSubsystemAsync_ServerRefuses_ReturnsFalse()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelFailure, .. UInt32(0)]));

        Assert.IsFalse(await peer.Channel.RequestSubsystemAsync("sftp", CancellationToken.None));
    }

    [TestMethod]
    public async Task RequestExecAsync_ServerAccepts_SendsTheCommandWithAReplyWantedAsLibssh2Does()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)]));

        Assert.IsTrue(await peer.Channel.RequestExecAsync("scp -pf '/f'"u8.ToArray(), CancellationToken.None));

        CollectionAssert.AreEqual(
            Join([SshConnectionMessageNumber.ChannelRequest], ServerChannelBytes, Name("exec"), [1], Name("scp -pf '/f'")),
            SftpServerScript.SshPayloads(peer.Connection.Written)[1]);
    }

    [TestMethod]
    public async Task RequestExecAsync_ServerRefuses_ReturnsFalse()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelFailure, .. UInt32(0)]));

        Assert.IsFalse(await peer.Channel.RequestExecAsync("scp -pf '/f'"u8.ToArray(), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_DataLargerThanTheServersPacket_SplitsItIntoPacketsOfThatSize()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm(maximumPacketSize: 10));
        byte[] data = [.. Enumerable.Range(0, 25).Select(value => (byte)value)];

        await peer.Channel.SendAsync(data, CancellationToken.None);

        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(4, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[..10])), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[10..20])), written[2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[20..])), written[3]);
    }

    [TestMethod]
    public async Task SendAsync_ServersWindowUsedUp_WaitsForAWindowAdjustment()
    {
        Peer peer = await OpenedAsync(new SftpServerScript()
            .Confirm(window: 4)
            .Ssh(Join([SshConnectionMessageNumber.ChannelWindowAdjust], UInt32(0), UInt32(100))));
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        await peer.Channel.SendAsync(data, CancellationToken.None);

        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(3, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[..4])), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[4..])), written[2]);
    }

    [TestMethod]
    public async Task ReadAsync_DataInSeveralPackets_ReadsItInOrderThenZeroAtEof()
    {
        Peer peer = await OpenedAsync(new SftpServerScript()
            .Confirm()
            .ChannelData([1, 2, 3])
            .ChannelData([])
            .ChannelData([4, 5])
            .Ssh([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)]));
        byte[] buffer = new byte[2];

        Assert.AreEqual(2, await peer.Channel.ReadAsync(buffer, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
        Assert.AreEqual(1, await peer.Channel.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(3, buffer[0]);
        Assert.AreEqual(2, await peer.Channel.ReadAsync(buffer, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, buffer);
        Assert.AreEqual(0, await peer.Channel.ReadAsync(buffer, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_ServerCloses_ReturnsZero()
    {
        Peer peer = await OpenedAsync(new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]));

        Assert.AreEqual(0, await peer.Channel.ReadAsync(new byte[4], CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_ExtendedDataAndChannelRequests_SkipsThemAndRefusesARequestThatWantsAReply()
    {
        Peer peer = await OpenedAsync(new SftpServerScript()
            .Confirm()
            .Ssh(Join([SshConnectionMessageNumber.ChannelExtendedData], UInt32(0), UInt32(1), Name("stderr text")))
            .Ssh(Join([SshConnectionMessageNumber.ChannelRequest], UInt32(0), Name("exit-status"), [0], UInt32(0)))
            .Ssh(Join([SshConnectionMessageNumber.ChannelRequest], UInt32(0), Name("keepalive@openssh.com"), [1]))
            .ChannelData([9]));
        byte[] buffer = new byte[4];

        Assert.AreEqual(1, await peer.Channel.ReadAsync(buffer, CancellationToken.None));

        Assert.AreEqual(9, buffer[0]);
        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(2, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelFailure], ServerChannelBytes), written[1]);
    }

    [TestMethod]
    public async Task ReadAsync_WindowFallsBelowThreeQuarters_RestoresTheWholeWindow()
    {
        SftpServerScript script = new SftpServerScript().Confirm();
        for (int packet = 0; packet < 18; packet++)
        {
            script.ChannelData(new byte[30000]);
        }

        Peer peer = await OpenedAsync(script);
        byte[] buffer = new byte[30000];
        for (int packet = 0; packet < 18; packet++)
        {
            Assert.AreEqual(30000, await peer.Channel.ReadAsync(buffer, CancellationToken.None));
        }

        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(2, written, "17 packets leave 1587152 bytes, above 1572864; the 18th crosses it");
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelWindowAdjust], ServerChannelBytes, UInt32(540000)), written[1]);
    }

    [TestMethod]
    public async Task CloseAsync_SendsEofWaitsForTheServersCloseThenClosesAsMeasured()
    {
        Peer peer = await OpenedAsync(new SftpServerScript()
            .Confirm()
            .Ssh([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)])
            .Ssh([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]));

        await peer.Channel.CloseAsync(CancellationToken.None);

        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(3, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], ServerChannelBytes), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], ServerChannelBytes), written[2]);
    }

    [TestMethod]
    public async Task OpenAsync_ServerStartsAKeyReExchange_AnswersItAndCarriesOn()
    {
        TestHostKey hostKey = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        SshKexInit firstKexInit = ServerKexInit("ecdh-sha2-nistp256", hostKey.Algorithm);
        SshKexInit secondKexInit = ServerKexInit("diffie-hellman-group14-sha256", hostKey.Algorithm);
        TestEphemeralKeys keys = new();
        TestKeyExchangeServer first = TestKeyExchangeServer.Answer("ecdh-sha2-nistp256", hostKey, keys, ClientKexInit, firstKexInit.ToPayload());
        TestKeyExchangeServer second = TestKeyExchangeServer.Answer("diffie-hellman-group14-sha256", hostKey, keys, ClientKexInit, secondKexInit.ToPayload());
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(firstKexInit);
        first.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(ctr, first.Keys(first.ExchangeHash)), resetSequenceNumber: false)
            .KexInit(secondKexInit);
        second.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(ctr, second.Keys(first.ExchangeHash)), resetSequenceNumber: false)
            .Packet(Join([SshConnectionMessageNumber.ChannelOpenConfirmation], UInt32(0), UInt32(7), UInt32(100), UInt32(100)));
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        SshSessionChannel channel = new(transport);

        Assert.IsTrue(await channel.OpenAsync(CancellationToken.None));

        List<byte[]> written = await SshClientTranscript.PayloadsAsync(
            connection.Written,
            false,
            SshPacketProtections.ForClientToServer(ctr, first.Keys(first.ExchangeHash)),
            SshPacketProtections.ForClientToServer(ctr, second.Keys(first.ExchangeHash)));
        CollectionAssert.AreEqual(ClientKexInit, written[4], "the client answers the server's KEXINIT with its own");
    }

    private static SshKexInit ServerKexInit(string method, string hostKey) =>
        SshServerScript.OpenSshKexInit(kexInit => kexInit with
        {
            KeyExchange = [method],
            ServerHostKey = [hostKey],
            CipherClientToServer = ["aes128-ctr"],
            CipherServerToClient = ["aes128-ctr"],
            MacClientToServer = ["hmac-sha2-256"],
            MacServerToClient = ["hmac-sha2-256"],
        });

    private static Peer Connect(SftpServerScript script)
    {
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), new TestEphemeralKeys());
        return new Peer(new SshSessionChannel(transport), connection);
    }

    private static async Task<Peer> OpenedAsync(SftpServerScript script)
    {
        Peer peer = Connect(script);
        Assert.IsTrue(await peer.Channel.OpenAsync(CancellationToken.None));
        return peer;
    }

    private static void AssertWritten(Peer peer, params byte[][] expected)
    {
        List<byte[]> written = SftpServerScript.SshPayloads(peer.Connection.Written);
        Assert.HasCount(expected.Length, written);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], written[index], $"client message {index}");
        }
    }

    private sealed record Peer(SshSessionChannel Channel, ScriptedConnection Connection);
}
