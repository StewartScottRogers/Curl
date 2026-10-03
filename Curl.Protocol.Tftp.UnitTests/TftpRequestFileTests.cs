using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins the file name and transfer mode of the read and write requests, and the requests
/// curl refuses to send, against curl 8.21.0 (mingw, Schannel), measured 2026-10-02 with
/// <c>Record-CurlExchange.ps1 -Tftp</c> (BL-1238 Notes): the name is the URL path's
/// percent-decoded bytes, the mode <c>netascii</c> under <c>-B</c> or a
/// <c>;mode=netascii</c> suffix, and a decoded NUL, a name too long for the 512-byte
/// packet and options that no longer fit end the transfer with no datagram sent.
/// </summary>
[TestClass]
public sealed class TftpRequestFileTests
{
    private const string Options = "tsize\00\0blksize\0512\0timeout\06\0";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_PercentE9Name_SendsTheRawByte()
    {
        var channel = Download();

        await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context("tftp://h/%E9.txt"));

        CollectionAssert.AreEqual(
            Bytes($"\0\u0001é.txt\0octet\0{Options}"),
            channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h/f.txt", true, "netascii")]
    [DataRow("tftp://h/f.txt;mode=netascii", false, "netascii")]
    [DataRow("tftp://h/f.txt;mode=octet", true, "octet")]
    [DataRow("tftp://h/f.txt;mode=octet", false, "octet")]
    [DataRow("tftp://h/f.txt", false, "octet")]
    public async Task ExecuteAsync_Download_SendsTheModeTheSuffixOrUseAsciiChooses(string url, bool useAscii, string mode)
    {
        var channel = Download();

        await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context(url, useAscii: useAscii));

        CollectionAssert.AreEqual(Bytes($"\0\u0001f.txt\0{mode}\0{Options}"), channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h/f.txt;mode=netascii", false, "netascii")]
    [DataRow("tftp://h/f.txt", true, "netascii")]
    [DataRow("tftp://h/f.txt;mode=octet", true, "octet")]
    public async Task ExecuteAsync_Upload_SendsTheModeTheSuffixOrUseAsciiChooses(string url, bool useAscii, string mode)
    {
        var channel = new ScriptedDatagramChannel(ServerEndPoint, [([0, 4, 0, 0], TransferEndPoint), ([0, 4, 0, 1], TransferEndPoint)]);

        var result = await new TftpProtocolHandler(Connector(channel))
            .ExecuteAsync(Context(url, useAscii: useAscii, upload: new MemoryStream("abc"u8.ToArray())));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            Bytes($"\0\u0002f.txt\0{mode}\0tsize\03\0blksize\0512\0timeout\06\0"),
            channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h/a%4", "a%4")]
    [DataRow("tftp://h/a%zz", "a%zz")]
    [DataRow("tftp://h/a%4z", "a%4z")]
    [DataRow("tftp://h/%4a%4A%2E", "JJ.")]
    public async Task ExecuteAsync_PercentEscapes_AreDecodedAsCurlDecodesThem(string url, string name)
    {
        var channel = Download();

        await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context(url));

        CollectionAssert.AreEqual(Bytes($"\0\u0001{name}\0octet\0{Options}"), channel.Sent[0].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_ModeSuffixAlone_ReturnsMissingFilename()
    {
        var connector = Connector(Download());

        var result = await new TftpProtocolHandler(connector).ExecuteAsync(Context("tftp://h/;mode=netascii"));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual("Missing filename", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_DecodedNul_ReturnsExit3AndSendsNothing(bool isUpload)
    {
        var channel = Download();
        var events = new RecordingTransferEvents();

        var result = await new TftpProtocolHandler(Connector(channel))
            .ExecuteAsync(Context("tftp://h/a%00b", events: events, upload: isUpload ? new MemoryStream() : null));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.IsEmpty(channel.Sent);
        Assert.AreEqual("shutting down connection #0", events.Steps[^1]);
        Assert.StartsWith("set timeouts for state 0;", events.Steps[^2]);
    }

    [TestMethod]
    [DataRow(504, false, false)]
    [DataRow(504, true, false)]
    [DataRow(504, false, true)]
    public async Task ExecuteAsync_504CharacterName_ReturnsExit71FilenameTooLongAndSendsNothing(int length, bool noOptions, bool isUpload)
    {
        await AssertRefusedAsync(length, noOptions, isUpload, "TFTP filename too long");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_503CharacterNameWithOptions_ReturnsExit71BufferTooSmallAndSendsNothing(bool isUpload)
    {
        await AssertRefusedAsync(503, noOptions: false, isUpload, "TFTP buffer too small for options");
    }

    [TestMethod]
    public async Task ExecuteAsync_503CharacterNameWithTftpNoOptions_SendsA512ByteRequest()
    {
        string name = new('a', 503);
        var channel = Download();

        var result = await new TftpProtocolHandler(Connector(channel))
            .ExecuteAsync(Context($"tftp://h/{name}", noOptions: true));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(Bytes($"\0\u0001{name}\0octet\0"), channel.Sent[0].Datagram);
        Assert.HasCount(512, channel.Sent[0].Datagram);
    }

    private static async Task AssertRefusedAsync(int length, bool noOptions, bool isUpload, string message)
    {
        string url = $"tftp://h/{new string('a', length)}";
        var channel = Download();
        var events = new RecordingTransferEvents();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(
            Context(url, noOptions: noOptions, events: events, upload: isUpload ? new MemoryStream() : null));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsEmpty(channel.Sent);
        CollectionAssert.AreEqual(new[] { message, "shutting down connection #0" }, events.Steps[^2..]);
    }

    private static ScriptedDatagramChannel Download() =>
        new(ServerEndPoint, [([0, 3, 0, 1, .. "hi"u8], TransferEndPoint)]);

    private static RecordingDatagramConnector Connector(ScriptedDatagramChannel channel) =>
        new(DatagramOpenResult.Opened(channel));

    private static TransferContext Context(
        string url,
        bool useAscii = false,
        bool noOptions = false,
        RecordingTransferEvents? events = null,
        Stream? upload = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            UseAscii = useAscii,
            TftpNoOptions = noOptions,
            Events = events ?? new RecordingTransferEvents(),
            Upload = upload,
        };

    private static byte[] Bytes(string text) => System.Text.Encoding.Latin1.GetBytes(text);
}
