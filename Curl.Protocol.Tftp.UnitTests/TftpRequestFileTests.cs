using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_PercentE9Name_SendsTheRawByte()
    {
        var channel = Download();

        await RunAsync(channel, Context("tftp://h/%E9.txt"));

        var expected = Bytes($"\0\u0001é.txt\0octet\0{Options}");
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(
            expected,
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
        Diagnostics.Arrange("expected mode", mode);
        var channel = Download();

        await RunAsync(channel, Context(url, useAscii: useAscii));

        var expected = Bytes($"\0\u0001f.txt\0{mode}\0{Options}");
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h/f.txt;mode=netascii", false, "netascii")]
    [DataRow("tftp://h/f.txt", true, "netascii")]
    [DataRow("tftp://h/f.txt;mode=octet", true, "octet")]
    public async Task ExecuteAsync_Upload_SendsTheModeTheSuffixOrUseAsciiChooses(string url, bool useAscii, string mode)
    {
        Diagnostics.Arrange("expected mode", mode);
        var channel = Scripted([0, 4, 0, 0], [0, 4, 0, 1]);

        var result = await RunAsync(channel, Context(url, useAscii: useAscii, upload: new MemoryStream("abc"u8.ToArray())));

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        var expected = Bytes($"\0\u0002f.txt\0{mode}\0tsize\03\0blksize\0512\0timeout\06\0");
        Diagnostics.Diff("write request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(
            expected,
            channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h/a%4", "a%4")]
    [DataRow("tftp://h/a%zz", "a%zz")]
    [DataRow("tftp://h/a%4z", "a%4z")]
    [DataRow("tftp://h/%4a%4A%2E", "JJ.")]
    public async Task ExecuteAsync_PercentEscapes_AreDecodedAsCurlDecodesThem(string url, string name)
    {
        Diagnostics.Arrange("expected file name", name);
        var channel = Download();

        await RunAsync(channel, Context(url));

        var expected = Bytes($"\0\u0001{name}\0octet\0{Options}");
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    [TestMethod]
    [DataRow("tftp://h//271", "/271")]
    [DataRow("tftp://h///a/b", "//a/b")]
    [DataRow("tftp://h//", "/")]
    public async Task ExecuteAsync_DownloadPathWithFurtherLeadingSlashes_DropsOnlyTheSeparatorSlash(string url, string name)
    {
        Diagnostics.Arrange("expected file name", name);
        var channel = Download();

        await RunAsync(channel, Context(url));

        var expected = Bytes($"\0\u0001{name}\0octet\0{Options}");
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadPathWithDoubleSlash_DropsOnlyTheSeparatorSlash()
    {
        var channel = Scripted([0, 4, 0, 0], [0, 4, 0, 1]);

        var result = await RunAsync(channel, Context("tftp://h//test1243.txt", upload: new MemoryStream("abc"u8.ToArray())));

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        var expected = Bytes($"\0\u0002/test1243.txt\0octet\0tsize\03\0blksize\0512\0timeout\06\0");
        Diagnostics.Diff("write request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_ModeSuffixAlone_ReturnsMissingFilename()
    {
        var channel = Download();

        var result = await RunAsync(channel, Context("tftp://h/;mode=netascii"));

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", "Missing filename", result.ErrorMessage);
        Assert.AreEqual("Missing filename", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_DecodedNul_ReturnsExit3AndSendsNothing(bool isUpload)
    {
        Diagnostics.Arrange("is upload", isUpload);
        var channel = Download();
        var events = new RecordingTransferEvents();

        var result = await RunAsync(channel, Context("tftp://h/a%00b", events: events, upload: isUpload ? new MemoryStream() : null));

        Diagnostics.Act("transfer event steps", events.Steps.Count);
        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Diagnostics.Assert("error message", "URL using bad/illegal format or missing URL", result.ErrorMessage);
        Assert.AreEqual("URL using bad/illegal format or missing URL", result.ErrorMessage);
        Diagnostics.Assert("datagrams sent", 0, channel.Sent.Count);
        Assert.IsEmpty(channel.Sent);
        Diagnostics.Assert("last step", "shutting down connection #0", events.Steps[^1]);
        Assert.AreEqual("shutting down connection #0", events.Steps[^1]);
        Diagnostics.Act("second to last step", events.Steps[^2]);
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

        var result = await RunAsync(channel, Context($"tftp://h/{name}", noOptions: true));

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        var expected = Bytes($"\0\u0001{name}\0octet\0");
        Diagnostics.Diff("read request", expected, channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(expected, channel.Sent[0].Datagram);
        Diagnostics.Assert("request length", 512, channel.Sent[0].Datagram.Length);
        Assert.HasCount(512, channel.Sent[0].Datagram);
    }

    private async Task AssertRefusedAsync(int length, bool noOptions, bool isUpload, string message)
    {
        Diagnostics.Arrange("name length", length);
        Diagnostics.Arrange("expected message", message);
        string url = $"tftp://h/{new string('a', length)}";
        var channel = Download();
        var events = new RecordingTransferEvents();

        var result = await RunAsync(
            channel,
            Context(url, noOptions: noOptions, events: events, upload: isUpload ? new MemoryStream() : null));

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(message, result.ErrorMessage);
        Diagnostics.Assert("datagrams sent", 0, channel.Sent.Count);
        Assert.IsEmpty(channel.Sent);
        Diagnostics.Act("last two steps", string.Join(" | ", events.Steps[^2..]));
        CollectionAssert.AreEqual(new[] { message, "shutting down connection #0" }, events.Steps[^2..]);
    }

    private async Task<TransferResult> RunAsync(ScriptedDatagramChannel channel, TransferContext context)
    {
        TransferResult result;
        using (Diagnostics.Phase("execute"))
        {
            result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        TftpTestDiagnostics.Sent(Diagnostics, channel);
        return result;
    }

    private ScriptedDatagramChannel Download() => Scripted([0, 3, 0, 1, .. "hi"u8]);

    private ScriptedDatagramChannel Scripted(params byte[][] datagrams)
    {
        for (int index = 0; index < datagrams.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, datagrams[index], TransferEndPoint);
        }

        return new(ServerEndPoint, [.. datagrams.Select(datagram => (datagram, (EndPoint)TransferEndPoint))]);
    }

    private static RecordingDatagramConnector Connector(ScriptedDatagramChannel channel) =>
        new(DatagramOpenResult.Opened(channel));

    private TransferContext Context(
        string url,
        bool useAscii = false,
        bool noOptions = false,
        RecordingTransferEvents? events = null,
        Stream? upload = null)
    {
        Diagnostics.Arrange("url", url.Length > 80 ? url[..80] + "..." : url);
        Diagnostics.Arrange("url length", url.Length);
        Diagnostics.Arrange("use ASCII", useAscii);
        Diagnostics.Arrange("--tftp-no-options", noOptions);
        Diagnostics.Arrange("upload", upload is not null);
        return new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            UseAscii = useAscii,
            TftpNoOptions = noOptions,
            Events = events ?? new RecordingTransferEvents(),
            Upload = upload,
        };
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.Latin1.GetBytes(text);
}
