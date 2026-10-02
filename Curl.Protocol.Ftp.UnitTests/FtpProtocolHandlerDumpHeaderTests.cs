using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins what an <c>ftp://</c> transfer writes to <see cref="ITransferContext.DumpHeaderOutput" />,
/// the <c>-D</c> stream alone, against curl 8.21.0 (the Schannel build), measured on
/// 2026-10-01 with <c>Record-CurlExchange.ps1 -Ftp -FtpData 'hello\r\n'</c> (BL-1131): every
/// control reply line, continuation lines included, byte for byte in arrival order, but not
/// <c>QUIT</c>'s reply, no command and no data-connection byte. Under <c>-i</c> alone nothing
/// of it reaches standard output.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerDumpHeaderTests
{
    private const string FileUrl = "ftp://127.0.0.1:18321/f.txt";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    /// <summary>Its replies to a download of the seven-byte <c>f.txt</c>, <c>QUIT</c>'s excluded.</summary>
    private const string Downloaded = LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 7\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputAndAMultiLineGreeting_WritesEveryReplyLineAndNothingToOutput()
    {
        // curl -s -D <file> ftp://..., replies "220-multi", "220 hi", "530 no": exit 67, the
        // file holds exactly the three lines.
        var dump = new MemoryStream();
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, "220-multi\r\n220 hi\r\n530 no\r\n", adjust: c => WithDump(c, dump));

        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual("220-multi\r\n220 hi\r\n530 no\r\n", Text(dump));
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputOnADownload_WritesCurlsDumpFileByteForByte()
    {
        // curl -s -D <file> ftp://.../f.txt: every reply but QUIT's 221, and the file itself
        // on standard output only.
        var dump = new MemoryStream();
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, Downloaded + Bye, "hello\r\n", c => WithDump(c, dump));

        Assert.AreEqual(TransferResult.Success(7), run.Result);
        Assert.AreEqual(Downloaded, Text(dump));
        Assert.AreEqual("hello\r\n", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputOnAListing_WritesCurlsDumpFileByteForByte()
    {
        // curl -s -D <file> ftp://.../dir/: CWD's 250 in its place, TYPE A's 200.
        const string Listed = LoggedIn + "250 OK\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";
        var dump = new MemoryStream();
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/dir/", Listed + Bye, "hello\r\n", c => WithDump(c, dump));

        Assert.AreEqual(TransferResult.Success(7), run.Result);
        Assert.AreEqual(Listed, Text(dump));
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeWithoutDumpHeader_WritesNoReplyLineToOutput()
    {
        // curl -s -i ftp://.../f.txt: standard output holds the file alone.
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, Downloaded + Bye, "hello\r\n", c => new TransferContext { Url = c.Url, Output = c.Output, HeaderOutput = c.Output });

        Assert.AreEqual(TransferResult.Success(7), run.Result);
        Assert.AreEqual("hello\r\n", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithDumpHeader_InterleavesReplyLinesAndCurlsHeaderLines()
    {
        // curl -s -I -D <file> ftp://.../f.txt: each synthesised line follows the reply it
        // came from; under -D without -i both streams are the -D file.
        var dump = new MemoryStream();
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            LoggedIn + "213 20260927123456\r\n200 Type set\r\n213 7\r\n350 Restarting at 0\r\n" + Bye,
            adjust: c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true, HeaderOutput = dump, DumpHeaderOutput = dump });

        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.AreEqual(
            LoggedIn + "213 20260927123456\r\nLast-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\n200 Type set\r\n213 7\r\nContent-Length: 7\r\n350 Restarting at 0\r\nAccept-ranges: bytes\r\n",
            Text(dump));
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_OversizedReplyLine_IsNotWrittenToDumpHeaderOutput()
    {
        var dump = new MemoryStream();
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, "220 hi\r\n331 " + new string('x', 65536) + "\r\n", adjust: c => WithDump(c, dump));

        Assert.AreEqual(CurlExitCode.TooLarge, run.Result.ExitCode);
        Assert.AreEqual("220 hi\r\n", Text(dump));
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputRefusesAReplyLine_FailsWithExit23()
    {
        // Decided (BL-1131): as for -I's lines, curl's header callback fails the write.
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, "220 hi\r\n", adjust: c => WithDump(c, new WriteRefusingStream(new IOException("disk full"))));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "client returned ERROR on write of 8 bytes"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DumpHeaderOutputRefusesAborsReply_StillSucceeds()
    {
        // curl -r 0-4 -D <file>: the next reply, read as ABOR's once the outcome is decided,
        // is refused, and changes nothing.
        const string Sized = LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 10\r\n150 Opening BINARY mode data connection\r\n";
        var dump = new RefusingAfterStream(Encoding.Latin1.GetByteCount(Sized));
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            Sized + "226 Transfer complete\r\n" + Bye,
            "0123456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 4), DumpHeaderOutput = dump });

        StringAssert.EndsWith(run.Sent, "ABOR\r\nQUIT\r\n", StringComparison.Ordinal);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Sized, Text(dump));
    }

    private static TransferContext WithDump(TransferContext context, Stream dump) =>
        new() { Url = context.Url, Output = context.Output, DumpHeaderOutput = dump };

    private static string Text(MemoryStream stream) => Encoding.Latin1.GetString(stream.ToArray());

    /// <summary>Accepts writes until it holds <paramref name="capacity" /> bytes, then refuses every one.</summary>
    private sealed class RefusingAfterStream(int capacity) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            Length >= capacity ? throw new IOException("disk full") : base.WriteAsync(buffer, cancellationToken);
    }
}
