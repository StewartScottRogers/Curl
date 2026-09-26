using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins the <c>tftp://</c> download against curl 8.21.0's default behaviour, measured on
/// 2026-09-26 against a loopback UDP server that answered from a second port: the read
/// request bytes, where each acknowledgement goes, option acknowledgement handling, and
/// the exit code and message for every TFTP error code.
/// </summary>
[TestClass]
public sealed class TftpProtocolHandlerTests
{
    /// <summary>The read request curl 8.21.0 sent for <c>tftp://h/file.txt</c>.</summary>
    private static readonly byte[] ExpectedFileTxtReadRequest =
    [
        0, 1,
        .. "file.txt\0octet\0tsize\00\0blksize\0512\0timeout\06\0"u8,
    ];

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public void SupportedSchemes_IsExactlyTftp()
    {
        var handler = new TftpProtocolHandler(Connector(Channel()));

        CollectionAssert.AreEqual(new[] { "tftp" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TftpProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new TftpProtocolHandler(Connector(Channel()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_FileTxt_SendsCurlsDefaultReadRequestToServerEndPointOnPort69()
    {
        var channel = Channel(Data(1, "hello"));
        var connector = Connector(channel);

        await new TftpProtocolHandler(connector).ExecuteAsync(Context("tftp://h/file.txt"));

        CollectionAssert.AreEqual(new[] { ("h", 69) }, connector.Opens);
        CollectionAssert.AreEqual(ExpectedFileTxtReadRequest, channel.Sent[0].Datagram);
        Assert.AreEqual(ServerEndPoint, channel.Sent[0].Destination);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlNamesPort_OpensThatPort()
    {
        var connector = Connector(Channel(Data(1, "hello")));

        await new TftpProtocolHandler(connector).ExecuteAsync(Context("tftp://h:6969/file.txt"));

        CollectionAssert.AreEqual(new[] { ("h", 6969) }, connector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentEncodedPath_RequestsDecodedFileName()
    {
        var channel = Channel(Data(1, "hello"));

        await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/dir/my%20file.txt"));

        var request = channel.Sent[0].Datagram;
        Assert.AreEqual("dir/my file.txt", Encoding.UTF8.GetString(request, 2, "dir/my file.txt".Length));
    }

    [TestMethod]
    public async Task ExecuteAsync_ShortSingleBlock_AcksTransferEndPointAndWritesHello()
    {
        var channel = Channel(Data(1, "hello"));
        var output = new MemoryStream();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
        Assert.AreNotEqual(ServerEndPoint, channel.Sent[1].Destination);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThreeBlocks_AcksEachAndWritesAllBytesInOrder()
    {
        var first = Payload(512, 'a');
        var second = Payload(512, 'b');
        var third = Payload(100, 'c');
        var channel = Channel(Data(1, first), Data(2, second), Data(3, third));
        var output = new MemoryStream();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1124, result.BytesTransferred);
        Assert.AreEqual(first + second + third, Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 1, 2, 3 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgement_AcksZeroFirstAndUsesAcknowledgedBlockSize()
    {
        var channel = Channel(
            OptionAcknowledgement("blksize\01024\0"),
            Data(1, Payload(1024, 'a')),
            Data(2, Payload(512, 'b')));

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1536, result.BytesTransferred);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, AcknowledgedBlocks(channel));
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
    }

    [TestMethod]
    [DataRow("tsize\01234\0blksize\08\0", 8, DisplayName = "blksize after another option")]
    [DataRow("BLKSIZE\065464\0", 65464, DisplayName = "name is case-insensitive, largest size")]
    [DataRow("tsize\01234\0", 512, DisplayName = "no blksize keeps 512")]
    [DataRow("blksize\0abc\0", 512, DisplayName = "unparsable blksize keeps 512")]
    [DataRow("blksize\07\0", 512, DisplayName = "blksize below 8 keeps 512")]
    [DataRow("blksize\065465\0", 512, DisplayName = "blksize above 65464 keeps 512")]
    public async Task ExecuteAsync_OptionAcknowledgement_DecidesBlockSize(string options, int expectedBlockSize)
    {
        var channel = Channel(
            OptionAcknowledgement(options),
            Data(1, Payload(expectedBlockSize, 'a')),
            Data(2, string.Empty));

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expectedBlockSize, result.BytesTransferred);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    [DataRow((ushort)0, CurlExitCode.TftpIllegal, "TFTP: Illegal operation")]
    [DataRow((ushort)1, CurlExitCode.TftpNotFound, "TFTP: File Not Found")]
    [DataRow((ushort)2, CurlExitCode.TftpPerm, "TFTP: Access Violation")]
    [DataRow((ushort)3, CurlExitCode.RemoteDiskFull, "Disk full or allocation exceeded")]
    [DataRow((ushort)4, CurlExitCode.TftpIllegal, "TFTP: Illegal operation")]
    [DataRow((ushort)5, CurlExitCode.TftpUnknownId, "TFTP: Unknown transfer ID")]
    [DataRow((ushort)6, CurlExitCode.RemoteFileExists, "Remote file already exists")]
    [DataRow((ushort)7, CurlExitCode.TftpNoSuchUser, "TFTP: No such user")]
    [DataRow((ushort)8, CurlExitCode.AbortedByCallback, "Operation was aborted by an application callback")]
    [DataRow((ushort)9, CurlExitCode.AbortedByCallback, "Operation was aborted by an application callback")]
    public async Task ExecuteAsync_ErrorPacket_ReturnsCurlsExitCodeAndMessage(
        ushort tftpErrorCode,
        CurlExitCode expectedExitCode,
        string expectedMessage)
    {
        var channel = Channel(Error(tftpErrorCode, "server says no"));

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt"));

        Assert.AreEqual(expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFileName_ReturnsMissingFilenameWithoutOpening()
    {
        var connector = Connector(Channel());

        var result = await new TftpProtocolHandler(connector).ExecuteAsync(Context("tftp://h/"));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual("Missing filename", result.ErrorMessage);
        Assert.IsEmpty(connector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenFails_ReturnsConnectorsCodeAndMessageUnchanged()
    {
        var connector = new RecordingDatagramConnector(
            DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"));

        var result = await new TftpProtocolHandler(connector).ExecuteAsync(Context("tftp://h/file.txt"));

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: h", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShortDatagramUnexpectedOpcodeAndWrongBlock_AreIgnored()
    {
        var channel = Channel(
            ([0, 3], TransferEndPoint),
            ([0, 4, 0, 1], TransferEndPoint),
            ([0, 9, 0, 1], TransferEndPoint),
            Data(2, "early"),
            Data(1, "hello"));
        var output = new MemoryStream();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/file.txt", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(channel));
    }

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = new Uri(url), Output = output ?? new MemoryStream() };

    private static ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, script);

    private static RecordingDatagramConnector Connector(ScriptedDatagramChannel channel) =>
        new(DatagramOpenResult.Opened(channel));

    private static string Payload(int length, char fill) => new(fill, length);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) Error(ushort code, string message) =>
        ([0, 5, (byte)(code >> 8), (byte)code, .. Encoding.ASCII.GetBytes(message), 0], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);

    /// <summary>
    /// The block numbers of every acknowledgement sent, skipping the read request.
    /// </summary>
    private static ushort[] AcknowledgedBlocks(ScriptedDatagramChannel channel) =>
        [.. channel.Sent.Skip(1).Select(sent =>
        {
            CollectionAssert.AreEqual(new byte[] { 0, 4 }, sent.Datagram[..2]);
            return (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]);
        })];
}
