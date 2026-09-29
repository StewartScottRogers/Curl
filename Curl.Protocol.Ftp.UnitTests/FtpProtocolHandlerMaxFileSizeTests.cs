using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>--max-filesize</c> on <c>ftp://</c> against curl 8.21.0 (BL-638): a <c>SIZE</c>
/// count over the limit is exit 63 before <c>REST</c> or <c>RETR</c>, and a download whose
/// size is not known stops at the limit with exit 63 and no <c>QUIT</c>. Every case was
/// recorded from real curl on 2026-09-29 with <c>Record-CurlExchange.ps1 -Ftp -FtpData
/// 'Hello, world!\n'</c> (fourteen bytes) and is replayed here with the recorder's replies,
/// on a ten-byte file.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerMaxFileSizeTests
{
    private const string Url = "ftp://127.0.0.1:18321/dir/f.txt";

    private const string InDirectory = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n";

    private const string Typed = InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    /// <summary>The replies through <c>SIZE</c> for the ten-byte file.</summary>
    private const string Sized = Typed + "213 10\r\n";

    /// <summary>The replies through a <c>SIZE</c> the server does not understand.</summary>
    private const string Unsized = Typed + "500 Unknown command\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string SizeSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\n";

    private const string Data = "0123456789";

    [TestMethod]
    public async Task ExecuteAsync_SizeOverTheLimit_FailsWithExit63BeforeRetr()
    {
        // curl --max-filesize 5 on the fourteen-byte file: SIZE, QUIT, "Maximum file size exceeded".
        FtpRun run = await RunAsync(Sized + Bye, Data, 5);

        Assert.AreEqual(SizeSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual("", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Maximum file size exceeded", 0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeEqualToTheLimit_Downloads()
    {
        // curl --max-filesize 14 on the fourteen-byte file: exit 0.
        FtpRun run = await RunAsync(Sized + Opened + Bye, Data, 10);

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Data, run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeUnderTheLimit_Downloads()
    {
        // curl --max-filesize 100: exit 0.
        FtpRun run = await RunAsync(Sized + Opened + Bye, Data, 100);

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroLimit_MeansNoLimit()
    {
        FtpRun run = await RunAsync(Sized + Opened + Bye, Data, 0);

        Assert.AreEqual(Data, run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeWithTheWholeFileOverTheLimit_FailsBeforeRest()
    {
        // curl --max-filesize 10 -C 10 on the fourteen-byte file: the whole size is compared, not what is left.
        FtpRun run = await RunAsync(Sized + Bye, Data, 8, resumeFrom: 5);

        Assert.AreEqual(SizeSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Maximum file size exceeded", 0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeWithTheWholeFileOverTheLimit_SendsAborThenQuit()
    {
        // curl --max-filesize 10 -r 0-3 on the fourteen-byte file: SIZE, ABOR, QUIT.
        FtpRun run = await RunAsync(Sized + "502 Command not implemented\r\n" + Bye, Data, 8, range: ByteRange.Bounded(0, 3));

        Assert.AreEqual(SizeSent + "ABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Maximum file size exceeded", 0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeRefusedWith550_StillFailsWithExit78()
    {
        // curl --max-filesize 5 with SIZE answered 550: "The file does not exist", as without the limit.
        FtpRun run = await RunAsync(Typed + "550 No such file\r\n" + Bye, Data, 5);

        Assert.AreEqual(SizeSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist", 0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeUnknownAndDataOverTheLimit_StopsAtTheLimitWithoutQuit()
    {
        // curl --max-filesize 5 with SIZE answered 500: RETR, "Hello" written, no QUIT.
        FtpRun run = await RunAsync(Unsized + Opened + Bye, Data, 5);

        Assert.AreEqual(SizeSent + "RETR f.txt\r\n", run.Sent);
        Assert.AreEqual("01234", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (5) with 5 bytes", 5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeUnknownAndDataEqualToTheLimit_Downloads()
    {
        // curl --max-filesize 14 with SIZE answered 500: exit 0.
        FtpRun run = await RunAsync(Unsized + Opened + Bye, Data, 10);

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Data, run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeUnknownAndMoreArrivingOnceTheLimitIsFilled_WritesNothingMore()
    {
        // The limit filled by the first read exactly: the second read is refused whole, with no empty write.
        var output = new CountingStream();
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Unsized + Opened + Bye)),
            new ScriptedConnection(Encoding.Latin1.GetBytes("01234"), Encoding.Latin1.GetBytes("56789")),
            c => Limited(c, 5, output: output));

        Assert.AreEqual(SizeSent + "RETR f.txt\r\n", run.Sent);
        Assert.AreEqual(1, output.Writes);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (5) with 5 bytes", 5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeUnknownAndResumed_CountsTheLimitFromTheResumedByte()
    {
        // curl --max-filesize 5 -C 4 with SIZE answered 500: REST 4, "o, wo" written.
        FtpRun run = await RunAsync(Unsized + "350 Restarting\r\n" + Opened + Bye, "456789", 5, resumeFrom: 4);

        Assert.AreEqual(SizeSent + "REST 4\r\nRETR f.txt\r\n", run.Sent);
        Assert.AreEqual("45678", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (5) with 5 bytes", 5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingOverTheLimit_StopsAtTheLimitWithoutQuit()
    {
        // curl --max-filesize 5 ftp://host/: TYPE A, LIST, "Hello" written, no QUIT.
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir/",
            Typed + Opened + Bye,
            Data,
            c => Limited(c, 5));

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE A\r\nLIST\r\n", run.Sent);
        Assert.AreEqual("01234", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (5) with 5 bytes", 5), run.Result);
    }

    private static Task<FtpRun> RunAsync(string replies, string data, long maxFileSize, long? resumeFrom = null, ByteRange? range = null) =>
        FtpRun.ExecuteAsync(Url, replies, data, c => Limited(c, maxFileSize, resumeFrom, range));

    private static TransferContext Limited(TransferContext context, long maxFileSize, long? resumeFrom = null, ByteRange? range = null, Stream? output = null) =>
        new()
        {
            Url = context.Url,
            Output = output ?? context.Output,
            MaxFileSize = maxFileSize,
            ResumeFrom = resumeFrom,
            Range = range,
        };

    /// <summary>A memory stream that counts the writes made to it.</summary>
    private sealed class CountingStream : MemoryStream
    {
        public int Writes { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
