using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_IsExactlyTftp()
    {
        var handler = new TftpProtocolHandler(Connector(Channel()));
        Diagnostics.Arrange("handler", "a TFTP handler over a channel with nothing scripted");

        var schemes = handler.SupportedSchemes.ToArray();

        Diagnostics.Act("supported schemes", string.Join(",", schemes));
        Diagnostics.Assert("supported schemes", "tftp", string.Join(",", schemes));
        CollectionAssert.AreEqual(new[] { "tftp" }, schemes);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "null");

        var thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new TftpProtocolHandler(null!));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new TftpProtocolHandler(Connector(Channel()));
        Diagnostics.Arrange("context", "null");

        var thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileTxt_SendsCurlsDefaultReadRequestToServerEndPointOnPort69()
    {
        var channel = Channel(Data(1, "hello"));
        var connector = Connector(channel);

        await RunAsync(connector, Context("tftp://h/file.txt"), channel);

        Diagnostics.Act("opens", string.Join(";", connector.Opens));
        Diagnostics.Assert("opens", "(h, 69)", string.Join(";", connector.Opens));
        CollectionAssert.AreEqual(new[] { ("h", 69) }, connector.Opens);
        Diagnostics.Diff("read request", ExpectedFileTxtReadRequest, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(ExpectedFileTxtReadRequest, channel.Sent[0].Datagram);
        Diagnostics.Assert("request destination", ServerEndPoint, channel.Sent[0].Destination);
        Assert.AreEqual(ServerEndPoint, channel.Sent[0].Destination);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlNamesPort_OpensThatPort()
    {
        var channel = Channel(Data(1, "hello"));
        var connector = Connector(channel);

        await RunAsync(connector, Context("tftp://h:6969/file.txt"), channel);

        Diagnostics.Assert("opens", "(h, 6969)", string.Join(";", connector.Opens));
        CollectionAssert.AreEqual(new[] { ("h", 6969) }, connector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentEncodedPath_RequestsDecodedFileName()
    {
        var channel = Channel(Data(1, "hello"));

        await RunAsync(Connector(channel), Context("tftp://h/dir/my%20file.txt"), channel);

        var request = channel.Sent[0].Datagram;
        string actualName = Encoding.UTF8.GetString(request, 2, "dir/my file.txt".Length);
        Diagnostics.Assert("requested file name", "dir/my file.txt", actualName);
        Assert.AreEqual("dir/my file.txt", actualName);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShortSingleBlock_AcksTransferEndPointAndWritesHello()
    {
        var channel = Channel(Data(1, "hello"));
        var output = new MemoryStream();

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", output), channel);

        Diagnostics.Bytes("output", output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 5, result.BytesTransferred);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Diff("acknowledgement", new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1].Datagram);
        Diagnostics.Assert("acknowledgement destination", TransferEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
        Assert.AreNotEqual(ServerEndPoint, channel.Sent[1].Destination);
        Diagnostics.Assert("channel disposed", true, channel.IsDisposed);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithNoPort_OpensPort69()
    {
        var channel = Channel(Data(1, "hello"));
        var connector = Connector(channel);

        await RunAsync(connector, Context("unknown://h/file.txt", new MemoryStream()), channel);

        Diagnostics.Assert("opens", "(h, 69)", string.Join(";", connector.Opens));
        CollectionAssert.AreEqual(new[] { ("h", 69) }, connector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThreeBlocks_AcksEachAndWritesAllBytesInOrder()
    {
        var first = Payload(512, 'a');
        var second = Payload(512, 'b');
        var third = Payload(100, 'c');
        var channel = Channel(Data(1, first), Data(2, second), Data(3, third));
        var output = new MemoryStream();

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", output), channel);

        Diagnostics.Bytes("output", output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 1124, result.BytesTransferred);
        Assert.AreEqual(1124, result.BytesTransferred);
        Assert.AreEqual(first + second + third, Encoding.ASCII.GetString(output.ToArray()));
        var acknowledged = AcknowledgedBlocks(channel);
        Diagnostics.Assert("acknowledged blocks", "1,2,3", string.Join(",", acknowledged));
        CollectionAssert.AreEqual(new ushort[] { 1, 2, 3 }, acknowledged);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgement_AcksZeroFirstAndUsesAcknowledgedBlockSize()
    {
        var channel = Channel(
            OptionAcknowledgement("blksize\01024\0"),
            Data(1, Payload(1024, 'a')),
            Data(2, Payload(512, 'b')));

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", tftpBlockSize: 1024), channel);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 1536, result.BytesTransferred);
        Assert.AreEqual(1536, result.BytesTransferred);
        var acknowledged = AcknowledgedBlocks(channel);
        Diagnostics.Assert("acknowledged blocks", "0,1,2", string.Join(",", acknowledged));
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, acknowledged);
        Diagnostics.Assert("second datagram destination", TransferEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
    }

    /// <summary>
    /// The OACK cases are pinned from curl 8.21.0's source, <c>tftp_parse_option_ack</c> in
    /// <c>lib/tftp.c</c> lines 259-330 at tag <c>curl-8_21_0</c>, because
    /// <c>Record-CurlExchange.ps1 -Tftp</c> cannot send a chosen OACK: a <c>blksize</c> up to
    /// the one requested is granted (its leading digits, trailing text ignored), an
    /// acknowledgement without one keeps 512, and a <c>tsize</c> that does not parse is ignored.
    /// </summary>
    [TestMethod]
    [DataRow("tsize\01234\0blksize\08\0", null, 8, DisplayName = "blksize after another option")]
    [DataRow("BLKSIZE\065464\0", 65464, 65464, DisplayName = "name is case-insensitive, largest size")]
    [DataRow("tsize\01234\0", null, 512, DisplayName = "no blksize keeps 512")]
    [DataRow("blksize\0512\0", null, 512, DisplayName = "blksize equal to the default 512 requested")]
    [DataRow("blksize\01024x\0", 1024, 1024, DisplayName = "1024x with --tftp-blksize 1024 is 1024")]
    [DataRow("tsize\0many\0", null, 512, DisplayName = "unparsable tsize is ignored")]
    [DataRow("tsize\099999999999999999999\0", null, 512, DisplayName = "tsize too large to read is ignored")]
    [DataRow("\0\0", null, 512, DisplayName = "an empty name and value are ignored")]
    public async Task ExecuteAsync_OptionAcknowledgement_DecidesBlockSize(string options, int? tftpBlockSize, int expectedBlockSize)
    {
        Diagnostics.Arrange("option acknowledgement", options.Replace('\0', '|'));
        Diagnostics.Arrange("expected block size", expectedBlockSize);
        var channel = Channel(
            OptionAcknowledgement(options),
            Data(1, Payload(expectedBlockSize, 'a')),
            Data(2, string.Empty));

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", tftpBlockSize: tftpBlockSize), channel);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", expectedBlockSize, result.BytesTransferred);
        Assert.AreEqual(expectedBlockSize, result.BytesTransferred);
        var acknowledged = AcknowledgedBlocks(channel);
        Diagnostics.Assert("acknowledged blocks", "0,1,2", string.Join(",", acknowledged));
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, acknowledged);
    }

    /// <summary>
    /// curl 8.21.0's <c>tftp_parse_option_ack</c> (<c>lib/tftp.c</c> lines 259-330 at tag
    /// <c>curl-8_21_0</c>) rejects these acknowledgements with exit 71 and returns at once
    /// from <c>tftp_receive_packet</c>, so neither an ACK nor an ERROR packet follows the
    /// request. Pinned from the source because <c>Record-CurlExchange.ps1 -Tftp</c> cannot
    /// send a chosen OACK.
    /// </summary>
    [TestMethod]
    [DataRow("blksize\0abc\0", "blksize is larger than max supported (65464)", DisplayName = "unparsable blksize")]
    [DataRow("blksize\065465\0", "blksize is larger than max supported (65464)", DisplayName = "blksize above 65464")]
    [DataRow("blksize\00\0", "invalid blocksize value in OACK packet", DisplayName = "blksize 0")]
    [DataRow("blksize\07\0", "blksize is smaller than min supported (8)", DisplayName = "blksize below 8")]
    [DataRow("blksize\01024\0", "server requested blksize larger than allocated (1024)", DisplayName = "blksize above the 512 requested")]
    [DataRow("tsize\00\0", "invalid tsize -::- value in OACK packet", DisplayName = "tsize 0")]
    [DataRow("tsize\00abc\0", "invalid tsize -:abc:- value in OACK packet", DisplayName = "tsize 0 with trailing text")]
    [DataRow("blksize\0512", "Malformed ACK packet, rejecting", DisplayName = "value without its terminator")]
    [DataRow("blksize\0", "Malformed ACK packet, rejecting", DisplayName = "name with no value after it")]
    [DataRow("blksize", "Malformed ACK packet, rejecting", DisplayName = "name without its terminator")]
    [DataRow("tsize\05\0blksize\0", "Malformed ACK packet, rejecting", DisplayName = "malformed after a good option")]
    public async Task ExecuteAsync_OptionAcknowledgementCurlRejects_ReturnsExit71AndSendsNothingMore(string options, string expectedMessage)
    {
        Diagnostics.Arrange("option acknowledgement", options.Replace('\0', '|'));
        Diagnostics.Arrange("expected message", expectedMessage);
        var channel = Channel(OptionAcknowledgement(options), Data(1, "hello"));

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt"), channel);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("numeric exit code", 71, (int)result.ExitCode);
        Assert.AreEqual(71, (int)result.ExitCode);
        Diagnostics.Assert("error message", expectedMessage, result.ErrorMessage);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Diagnostics.Assert("datagrams sent", 1, channel.Sent.Count);
        Assert.HasCount(1, channel.Sent);
        Diagnostics.Assert("channel disposed", true, channel.IsDisposed);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlockSizeAboveTheOneRequestedWithTftpBlockSize_ReturnsExit71()
    {
        var channel = Channel(OptionAcknowledgement("blksize\02048\0"));

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", tftpBlockSize: 1024), channel);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", "server requested blksize larger than allocated (2048)", result.ErrorMessage);
        Assert.AreEqual("server requested blksize larger than allocated (2048)", result.ErrorMessage);
        Diagnostics.Assert("datagrams sent", 1, channel.Sent.Count);
        Assert.HasCount(1, channel.Sent);
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
        Diagnostics.Arrange("TFTP error code", tftpErrorCode);
        var channel = Channel(Error(tftpErrorCode, "server says no"));

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt"), channel);

        Diagnostics.Assert("exit code", expectedExitCode, result.ExitCode);
        Assert.AreEqual(expectedExitCode, result.ExitCode);
        Diagnostics.Assert("error message", expectedMessage, result.ErrorMessage);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Diagnostics.Assert("channel disposed", true, channel.IsDisposed);
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFileName_ReturnsMissingFilenameWithoutOpening()
    {
        var connector = Connector(Channel());

        var result = await RunAsync(connector, Context("tftp://h/"));

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", "Missing filename", result.ErrorMessage);
        Assert.AreEqual("Missing filename", result.ErrorMessage);
        Diagnostics.Assert("opens", 0, connector.Opens.Count);
        Assert.IsEmpty(connector.Opens);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenFails_ReturnsConnectorsCodeAndMessageUnchanged()
    {
        Diagnostics.Arrange("connector result", "Failed(CouldntResolveHost, Could not resolve host: h)");
        var connector = new RecordingDatagramConnector(
            DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"));

        var result = await RunAsync(connector, Context("tftp://h/file.txt"));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Assert("error message", "Could not resolve host: h", result.ErrorMessage);
        Assert.AreEqual("Could not resolve host: h", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstReplyOfTheWrongBlock_IsIgnored()
    {
        var channel = Channel(
            Data(2, "early"),
            Data(1, "hello"));
        var output = new MemoryStream();

        var result = await RunAsync(Connector(channel), Context("tftp://h/file.txt", output), channel);

        Diagnostics.Bytes("output", output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        var acknowledged = AcknowledgedBlocks(channel);
        Diagnostics.Assert("acknowledged blocks", "1", string.Join(",", acknowledged));
        CollectionAssert.AreEqual(new ushort[] { 1 }, acknowledged);
    }

    private async Task<TransferResult> RunAsync(
        RecordingDatagramConnector connector,
        TransferContext context,
        ScriptedDatagramChannel? channel = null)
    {
        TransferResult result;
        using (Diagnostics.Phase("execute"))
        {
            result = await new TftpProtocolHandler(connector).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        if (channel is not null)
        {
            TftpTestDiagnostics.Sent(Diagnostics, channel);
        }

        return result;
    }

    private TransferContext Context(string url, Stream? output = null, int? tftpBlockSize = null)
    {
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("TFTP block size", tftpBlockSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(default)");
        return new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream(), TftpBlockSize = tftpBlockSize };
    }

    private ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (int index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, script);
    }

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
