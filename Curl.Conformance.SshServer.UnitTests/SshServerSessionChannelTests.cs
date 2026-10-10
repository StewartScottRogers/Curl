using System.Text;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerSessionChannelTests
{
    private const uint ClientChannel = 7;

    [TestMethod]
    public async Task OpenAsync_SessionThenRefusedRequestsThenSubsystem_ConfirmsRefusesAndStartsTheSubsystem()
    {
        Session session = Session.Create();

        await session.Client.WriteAsync(Open("session", 1000, 100), CancellationToken.None);
        await session.Client.WriteAsync(ChannelRequest("pty-req", wantReply: true, value: null), CancellationToken.None);
        await session.Client.WriteAsync(ChannelRequest("env", wantReply: false, value: null), CancellationToken.None);
        await session.Client.WriteAsync(ChannelRequest("subsystem", wantReply: true, value: "sftp"), CancellationToken.None);
        SshServerSessionChannel channel = await SshServerSessionChannel.OpenAsync(session.Transport, "someone", CancellationToken.None);

        SshWireReader confirmation = new(await session.ReadAsync());
        Assert.AreEqual(SshConnectionMessageNumber.ChannelOpenConfirmation, confirmation.ReadByte());
        Assert.AreEqual(ClientChannel, confirmation.ReadUInt32());
        Assert.AreEqual(0u, confirmation.ReadUInt32());
        Assert.AreEqual(SshServerSessionChannel.InitialWindowSize, confirmation.ReadUInt32());
        Assert.AreEqual(SshServerSessionChannel.MaximumPacketSize, confirmation.ReadUInt32());
        CollectionAssert.AreEqual(ChannelMessage(SshConnectionMessageNumber.ChannelFailure), await session.ReadAsync());
        CollectionAssert.AreEqual(ChannelMessage(SshConnectionMessageNumber.ChannelSuccess), await session.ReadAsync());
        Assert.AreEqual("someone", channel.User);
        Assert.AreEqual("subsystem", channel.ProcessRequest);
        Assert.AreEqual("sftp", channel.Process);
    }

    [TestMethod]
    public async Task OpenAsync_ClientOpensAnotherChannelType_ThrowsInvalidDataException()
    {
        Session session = Session.Create();

        await session.Client.WriteAsync(Open("direct-tcpip", 1000, 100), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await SshServerSessionChannel.OpenAsync(session.Transport, "someone", CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_ClientSendsAnotherMessage_ThrowsInvalidDataException()
    {
        Session session = Session.Create();

        await session.Client.WriteAsync(new byte[] { SshMessageNumber.Ignore, 0, 0, 0, 0 }, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await SshServerSessionChannel.OpenAsync(session.Transport, "someone", CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteDataAsync_ClientWindowSmallerThanTheData_SendsWithinTheWindowAndWaitsForAdjust()
    {
        Session session = Session.Create();
        SshServerSessionChannel channel = await session.OpenExecAsync(window: 4, maximumPacketSize: 3);
        SshWireWriter adjust = Header(SshConnectionMessageNumber.ChannelWindowAdjust, 0);
        adjust.WriteUInt32(100);

        await session.Client.WriteAsync(adjust.ToArray(), CancellationToken.None);
        await channel.WriteDataAsync("0123456789"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual(Data("012"), await session.ReadAsync());
        CollectionAssert.AreEqual(Data("3"), await session.ReadAsync());
        CollectionAssert.AreEqual(Data("456"), await session.ReadAsync());
        CollectionAssert.AreEqual(Data("789"), await session.ReadAsync());
    }

    [TestMethod]
    public async Task ReadDataAsync_AdjustThenDataThenEofThenClose_ReturnsTheDataThenNothing()
    {
        Session session = Session.Create();
        SshServerSessionChannel channel = await session.OpenExecAsync(window: 0, maximumPacketSize: 100);
        SshWireWriter adjust = Header(SshConnectionMessageNumber.ChannelWindowAdjust, 0);
        adjust.WriteUInt32(5);
        SshWireWriter data = Header(SshConnectionMessageNumber.ChannelData, 0);
        data.WriteString("hello"u8);

        await session.Client.WriteAsync(adjust.ToArray(), CancellationToken.None);
        await session.Client.WriteAsync(data.ToArray(), CancellationToken.None);
        await session.Client.WriteAsync(Header(SshConnectionMessageNumber.ChannelEof, 0).ToArray(), CancellationToken.None);
        await session.Client.WriteAsync(Header(SshConnectionMessageNumber.ChannelClose, 0).ToArray(), CancellationToken.None);
        byte[] first = await channel.ReadDataAsync(CancellationToken.None);
        byte[] atEof = await channel.ReadDataAsync(CancellationToken.None);
        byte[] atClose = await channel.ReadDataAsync(CancellationToken.None);
        await channel.WriteDataAsync("abcde"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual("hello"u8.ToArray(), first);
        Assert.IsEmpty(atEof);
        Assert.IsEmpty(atClose);
        CollectionAssert.AreEqual(Data("abcde"), await session.ReadAsync());
    }

    [TestMethod]
    public async Task ReadDataAsync_ClientUsesHalfTheWindow_ServerRestoresTheWholeWindow()
    {
        Session session = Session.Create();
        SshServerSessionChannel channel = await session.OpenExecAsync(window: 100, maximumPacketSize: 100);
        int packets = (int)(SshServerSessionChannel.InitialWindowSize / 2 / SshServerSessionChannel.MaximumPacketSize) + 1;
        SshWireWriter data = Header(SshConnectionMessageNumber.ChannelData, 0);
        data.WriteString(new byte[SshServerSessionChannel.MaximumPacketSize]);

        for (int index = 0; index < packets; index++)
        {
            await session.Client.WriteAsync(data.ToArray(), CancellationToken.None);
            await channel.ReadDataAsync(CancellationToken.None);
        }

        SshWireReader adjust = new(await session.ReadAsync());
        Assert.AreEqual(SshConnectionMessageNumber.ChannelWindowAdjust, adjust.ReadByte());
        Assert.AreEqual(ClientChannel, adjust.ReadUInt32());
        Assert.AreEqual((uint)(packets * SshServerSessionChannel.MaximumPacketSize), adjust.ReadUInt32());
    }

    [TestMethod]
    public async Task ReadDataAsync_ClientSendsAnotherChannelMessage_ThrowsInvalidDataException()
    {
        Session session = Session.Create();
        SshServerSessionChannel channel = await session.OpenExecAsync(window: 100, maximumPacketSize: 100);

        await session.Client.WriteAsync(Header(SshConnectionMessageNumber.ChannelSuccess, 0).ToArray(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await channel.ReadDataAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task CloseAsync_ExitStatus_SendsExitStatusEofAndClose()
    {
        Session session = Session.Create();
        SshServerSessionChannel channel = await session.OpenExecAsync(window: 100, maximumPacketSize: 100);

        await channel.CloseAsync(3, CancellationToken.None);

        SshWireReader status = new(await session.ReadAsync());
        Assert.AreEqual(SshConnectionMessageNumber.ChannelRequest, status.ReadByte());
        Assert.AreEqual(ClientChannel, status.ReadUInt32());
        Assert.AreEqual("exit-status", status.ReadName());
        Assert.IsFalse(status.ReadBoolean());
        Assert.AreEqual(3u, status.ReadUInt32());
        CollectionAssert.AreEqual(ChannelMessage(SshConnectionMessageNumber.ChannelEof), await session.ReadAsync());
        CollectionAssert.AreEqual(ChannelMessage(SshConnectionMessageNumber.ChannelClose), await session.ReadAsync());
    }

    private static byte[] Open(string type, uint window, uint maximumPacketSize)
    {
        SshWireWriter open = new();
        open.WriteByte(SshConnectionMessageNumber.ChannelOpen);
        open.WriteString(Encoding.ASCII.GetBytes(type));
        open.WriteUInt32(ClientChannel);
        open.WriteUInt32(window);
        open.WriteUInt32(maximumPacketSize);
        return open.ToArray();
    }

    private static byte[] ChannelRequest(string type, bool wantReply, string? value)
    {
        SshWireWriter request = Header(SshConnectionMessageNumber.ChannelRequest, 0);
        request.WriteString(Encoding.ASCII.GetBytes(type));
        request.WriteBoolean(wantReply);
        if (value is not null)
        {
            request.WriteString(Encoding.UTF8.GetBytes(value));
        }

        return request.ToArray();
    }

    private static byte[] ChannelMessage(byte messageNumber) => Header(messageNumber, ClientChannel).ToArray();

    private static byte[] Data(string text)
    {
        SshWireWriter data = Header(SshConnectionMessageNumber.ChannelData, ClientChannel);
        data.WriteString(Encoding.ASCII.GetBytes(text));
        return data.ToArray();
    }

    private static SshWireWriter Header(byte messageNumber, uint channel)
    {
        SshWireWriter writer = new();
        writer.WriteByte(messageNumber);
        writer.WriteUInt32(channel);
        return writer;
    }

    private sealed record Session(SshServerTransport Transport, SshPacketWriter Client, SshPacketReader ClientReader)
    {
        internal static Session Create()
        {
            (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
            return new Session(new SshServerTransport(server, new SystemSshRandomSource()), new SshPacketWriter(client, new SystemSshRandomSource()), new SshPacketReader(new SshConnectionReader(client)));
        }

        internal async Task<byte[]> ReadAsync() => await ClientReader.ReadAsync(CancellationToken.None);

        // Opens the channel with an exec request that wants no reply, and reads the confirmation.
        internal async Task<SshServerSessionChannel> OpenExecAsync(uint window, uint maximumPacketSize)
        {
            await Client.WriteAsync(Open("session", window, maximumPacketSize), CancellationToken.None);
            await Client.WriteAsync(ChannelRequest("exec", wantReply: false, value: "scp -f /file"), CancellationToken.None);
            SshServerSessionChannel channel = await SshServerSessionChannel.OpenAsync(Transport, "someone", CancellationToken.None);
            await ReadAsync();
            Assert.AreEqual("scp -f /file", channel.Process);
            return channel;
        }
    }
}
