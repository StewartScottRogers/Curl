using System.Buffers.Binary;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class TftpServerConnectorTests
{
    private const string Hello = "<reply>\n<data>\nhello\n</data>\n</reply>\n";

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, TftpServerConnector.TransferPort);

    [TestMethod]
    public async Task OpenAsync_AnotherPort_FailsAsUnreachable()
    {
        TftpServerConnector tftp = new(ParsedTestCase.From(Hello), TimeProvider.System);

        DatagramOpenResult result = await tftp.OpenAsync("127.0.0.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(UnreachableDatagramConnector.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_HostName_ServesOnLoopback()
    {
        await using IDatagramChannel channel = (await new TftpServerConnector(ParsedTestCase.From(Hello), TimeProvider.System)
            .OpenAsync("localhost", TftpServerConnector.TftpPort, CancellationToken.None)).Channel!;

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, TftpServerConnector.TftpPort), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task ReadRequest_IsLoggedAndServedFromTheTransferPort()
    {
        (TftpServerConnector tftp, IDatagramChannel channel) = await OpenAsync(Hello);

        await RequestAsync(channel, 1, "/5\0OCTET\0tsize\00\0blksize\0512\0");
        (string reply, EndPoint source) = await ReceiveAsync(channel);
        await SendAsync(channel, Packet(4, 1));

        Assert.AreEqual("0003 0001 hello\n", reply);
        Assert.AreEqual(TransferEndPoint, source);
        Assert.AreEqual("opcode = 1\nmode = OCTET\ntsize = 0\nblksize = 512\nfilename = /5\n", Encoding.Latin1.GetString(tftp.ProtocolLog.Span));
    }

    [TestMethod]
    public async Task ReadRequest_LongFile_IsSentInBlocksAndResentOnAnEarlierAck()
    {
        string sections = $"<reply>\n<data nonewline=\"yes\">\n{new string('a', 600)}\n</data>\n</reply>\n";
        (_, IDatagramChannel channel) = await OpenAsync(sections);

        await RequestAsync(channel, 1, "/5\0octet\0");
        (string first, _) = await ReceiveAsync(channel);
        await SendAsync(channel, Packet(4, 1));
        (string second, _) = await ReceiveAsync(channel);
        await SendAsync(channel, Packet(4, 1));
        (string resent, _) = await ReceiveAsync(channel);

        Assert.AreEqual("0003 0001 " + new string('a', 512), first);
        Assert.AreEqual("0003 0002 " + new string('a', 88), second);
        Assert.AreEqual(second, resent);
    }

    [TestMethod]
    public async Task ReadRequest_ExactBlock_EndsWithAnEmptyBlock()
    {
        string sections = $"<reply>\n<data nonewline=\"yes\">\n{new string('b', 512)}\n</data>\n</reply>\n";
        (_, IDatagramChannel channel) = await OpenAsync(sections);

        await RequestAsync(channel, 1, "/5\0octet\0");
        await ReceiveAsync(channel);
        await SendAsync(channel, Packet(4, 1));

        Assert.AreEqual("0003 0002 ", (await ReceiveAsync(channel)).Reply);
    }

    [TestMethod]
    public async Task ReadRequest_Netascii_ConvertsLineEndings()
    {
        (_, IDatagramChannel channel) = await OpenAsync("<reply>\n<data crlf=\"yes\">\nx\n</data>\n</reply>\n");

        await RequestAsync(channel, 1, "/5\0netascii\0");

        Assert.AreEqual("0003 0001 x\r\0\r\n", (await ReceiveAsync(channel)).Reply);
    }

    [TestMethod]
    [DataRow("/50003", "0003 0001 three\n")]
    [DataRow("/20000", "0003 0001 hello\n")]
    [DataRow("/a/b/c12", "0003 0001 hello\n")]
    [DataRow("/7", "0003 0001 hello\n")]
    public async Task ReadRequest_PicksThePartTheNumberNames(string filename, string expected)
    {
        (_, IDatagramChannel channel) = await OpenAsync("<reply>\n<data>\nhello\n</data>\n<data3>\nthree\n</data3>\n</reply>\n");

        await RequestAsync(channel, 1, filename + "\0octet\0");

        Assert.AreEqual(expected, (await ReceiveAsync(channel)).Reply);
    }

    [TestMethod]
    public async Task ReadRequest_MissingPart_IsAnEmptyFile()
    {
        (_, IDatagramChannel channel) = await OpenAsync("<reply>\n<servercmd>\nwritedelay: x\nother\n</servercmd>\n</reply>\n");

        await RequestAsync(channel, 1, "/5\0octet\0");

        Assert.AreEqual("0003 0001 ", (await ReceiveAsync(channel)).Reply);
    }

    [TestMethod]
    public async Task ReadRequest_WriteDelay_WaitsOnTheClock()
    {
        (_, IDatagramChannel channel) = await OpenAsync("<reply>\n<servercmd>\nwritedelay: 2\n</servercmd>\n<data>\nhi\n</data>\n</reply>\n", new WaitSkippingTimeProvider());

        await RequestAsync(channel, 1, "/5\0octet\0");

        Assert.AreEqual("0003 0001 hi\n", (await ReceiveAsync(channel)).Reply);
    }

    [TestMethod]
    [DataRow("/invalid-file\0octet\0", "0005 0002 Access violation\0", "filename = /invalid-file\n")]
    [DataRow("invalid5\0octet\0", "0005 0002 Access violation\0", "filename = invalid5\n")]
    [DataRow("/99999999999\0octet\0", "0005 0002 Access violation\0", "filename = /99999999999\n")]
    [DataRow("/5\0mail\0", "0005 0004 Illegal TFTP operation\0", "filename = /5\n")]
    [DataRow("/5\0octet", "0005 0004 Illegal TFTP operation\0", "")]
    [DataRow("/5\0", "0005 0004 Illegal TFTP operation\0", "")]
    public async Task ReadRequest_ItCannotServe_IsAnsweredWithAnError(string request, string expected, string filenameLine)
    {
        (TftpServerConnector tftp, IDatagramChannel channel) = await OpenAsync(Hello);

        await RequestAsync(channel, 1, request);

        Assert.AreEqual(expected, (await ReceiveAsync(channel)).Reply);
        StringAssert.EndsWith(Encoding.Latin1.GetString(tftp.ProtocolLog.Span), filenameLine);
    }

    [TestMethod]
    public async Task WriteRequest_IsAcknowledgedBlockByBlock()
    {
        (TftpServerConnector tftp, IDatagramChannel channel) = await OpenAsync(Hello);

        await RequestAsync(channel, 2, "/5\0octet\0tsize\0600\0");
        string start = (await ReceiveAsync(channel)).Reply;
        await SendAsync(channel, [.. Packet(3, 1), .. new byte[512]]);
        string first = (await ReceiveAsync(channel)).Reply;
        await SendAsync(channel, [.. Packet(3, 9), 1]);
        await SendAsync(channel, [.. Packet(3, 1), .. new byte[512]]);
        string again = (await ReceiveAsync(channel)).Reply;
        await SendAsync(channel, [.. Packet(3, 2), 1]);
        string last = (await ReceiveAsync(channel)).Reply;

        Assert.AreEqual("0004 0000 ", start);
        Assert.AreEqual("0004 0001 ", first);
        Assert.AreEqual("0004 0001 ", again);
        Assert.AreEqual("0004 0002 ", last);
        Assert.AreEqual("opcode = 2\nmode = octet\ntsize = 600\nfilename = /5\n", Encoding.Latin1.GetString(tftp.ProtocolLog.Span));
    }

    [TestMethod]
    public async Task WriteRequest_RecordsEachNewBlockOnceAsUploadedBytes()
    {
        (TftpServerConnector tftp, IDatagramChannel channel) = await OpenAsync(Hello);
        byte[] full = [.. Enumerable.Repeat((byte)'a', 512)];

        await RequestAsync(channel, 2, "/5\0octet\0");
        await SendAsync(channel, [.. Packet(3, 2), (byte)'x']);
        await SendAsync(channel, [.. Packet(3, 1), .. full]);
        await SendAsync(channel, [.. Packet(3, 1), .. full]);
        await SendAsync(channel, [.. Packet(3, 2), (byte)'b', (byte)'\n']);
        await SendAsync(channel, [.. Packet(3, 3), (byte)'y']);

        CollectionAssert.AreEqual((byte[])[.. full, (byte)'b', (byte)'\n'], tftp.UploadedBytes.ToArray());
    }

    [TestMethod]
    public async Task Datagrams_ThatAreNoRequestToTheListener_AreIgnored()
    {
        (TftpServerConnector tftp, IDatagramChannel channel) = await OpenAsync(Hello);

        await channel.SendAsync(new byte[] { 0, 1, 0 }, channel.ServerEndPoint, CancellationToken.None);
        await channel.SendAsync(Encoding.Latin1.GetBytes("\0\u0001/5\0octet\0"), TransferEndPoint, CancellationToken.None);
        await SendAsync(channel, Packet(9, 0));
        await SendAsync(channel, Packet(4, 0));
        await RequestAsync(channel, 1, "/5\0octet\0");

        Assert.AreEqual("0003 0001 hello\n", (await ReceiveAsync(channel)).Reply);
        Assert.AreEqual("opcode = 1\nmode = octet\nfilename = /5\n", Encoding.Latin1.GetString(tftp.ProtocolLog.Span));
    }

    private static async Task<(TftpServerConnector Tftp, IDatagramChannel Channel)> OpenAsync(string sections, TimeProvider? clock = null)
    {
        TftpServerConnector tftp = new(ParsedTestCase.From(sections), clock ?? TimeProvider.System);
        DatagramOpenResult result = await tftp.OpenAsync("127.0.0.1", TftpServerConnector.TftpPort, CancellationToken.None);
        return (tftp, result.Channel!);
    }

    private static ValueTask RequestAsync(IDatagramChannel channel, int opcode, string body) =>
        channel.SendAsync((byte[])[.. Packet(opcode, 0)[..2], .. Encoding.Latin1.GetBytes(body)], channel.ServerEndPoint, CancellationToken.None);

    private static ValueTask SendAsync(IDatagramChannel channel, byte[] datagram) =>
        channel.SendAsync(datagram, TransferEndPoint, CancellationToken.None);

    // The reply as "opcode block body", the two numbers in four hex digits each.
    private static async Task<(string Reply, EndPoint Source)> ReceiveAsync(IDatagramChannel channel)
    {
        byte[] buffer = new byte[1024];
        DatagramReceived received = await channel.ReceiveAsync(buffer, CancellationToken.None);
        string reply = $"{BinaryPrimitives.ReadUInt16BigEndian(buffer):x4} {BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(2)):x4} {Encoding.Latin1.GetString(buffer, 4, received.Length - 4)}";
        return (reply, received.RemoteEndPoint);
    }

    private static byte[] Packet(int opcode, int number)
    {
        byte[] packet = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(packet, (ushort)opcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)number);
        return packet;
    }
}
