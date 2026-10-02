using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>-r</c>, <c>-C</c> and <c>-I</c> on <c>ftp://</c> against curl 8.21.0: the
/// commands sent, the bytes written and the exit code and message of each outcome. Every
/// case was recorded from real curl on 2026-09-27 with <c>Record-CurlExchange.ps1 -Ftp
/// -FtpData 0123456789</c> against <c>ftp://127.0.0.1:port/dir/f.txt</c> (BL-438,
/// ADR-0323's addendum) and is replayed here with the recorder's replies. The recorder
/// serves the data from the last <c>REST</c> offset, as the scripted data does here.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerRangeTests
{
    private const string Url = "ftp://127.0.0.1:18321/dir/f.txt";

    /// <summary>The recording server's replies from the greeting through <c>CWD dir</c>.</summary>
    private const string InDirectory = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n";

    /// <summary>Its replies to <c>EPSV</c>, <c>TYPE I</c> and <c>SIZE</c> for the ten-byte file.</summary>
    private const string Sized = InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 10\r\n";

    private const string Restarting = "350 Restarting\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";

    private const string NotImplemented = "502 Command not implemented\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>What curl sent up to and including <c>SIZE f.txt</c>.</summary>
    private const string SizeSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\n";

    private const string LastModified = "Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\n";

    private const string SizeSentForHead = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nMDTM f.txt\r\nTYPE I\r\nSIZE f.txt\r\n";

    /// <summary>What curl 8.21.0 sent for <c>curl -I ftp://127.0.0.1:port/dir/f.txt</c>.</summary>
    private const string HeadSent = SizeSentForHead + "REST 0\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeFromZero_ReadsTheRangeThenSendsAbor()
    {
        // curl -r 0-4: no REST for offset 0, five bytes kept, then ABOR and QUIT.
        FtpRun run = await RunAsync(Sized + Opened + NotImplemented + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 4) });

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("01234", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeFromAnOffset_SendsRestThenAbor()
    {
        // curl -r 3-4.
        FtpRun run = await RunAsync(Sized + Restarting + Opened + NotImplemented + Bye, "3456789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(3, 4) });

        Assert.AreEqual(SizeSent + "REST 3\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("34", run.OutputText);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FromOffsetRange_SendsRestAndNoAbor()
    {
        // curl -r 5-.
        FtpRun run = await RunAsync(Sized + Restarting + Opened + Bye, "56789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.FromOffset(5) });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("56789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixRange_RestartsAtSizeLessTheSuffixThenSendsAbor()
    {
        // curl -r -3: REST 7 from SIZE 10.
        FtpRun run = await RunAsync(Sized + Restarting + Opened + NotImplemented + Bye, "789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Suffix(3) });

        Assert.AreEqual(SizeSent + "REST 7\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixAsLongAsTheFile_SendsRestZero()
    {
        // curl -r -10 on the ten-byte file: the offset resolves to 0 and REST 0 is still sent.
        FtpRun run = await RunAsync(Sized + Restarting + Opened + NotImplemented + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Suffix(10) });

        Assert.AreEqual(SizeSent + "REST 0\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixWithoutSize_SendsTheNegativeRestAndKeepsTheSuffixLength()
    {
        // curl -r -3 when SIZE is refused: REST -3; the recorder served from 0, curl kept three bytes.
        FtpRun run = await RunAsync(
            InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n502 No\r\n" + Restarting + Opened + NotImplemented + Bye,
            "0123456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Suffix(3) });

        Assert.AreEqual(SizeSent + "REST -3\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("012", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangePastTheEnd_ReadsToTheEndThenSendsAbor()
    {
        // curl -r 0-20 on the ten-byte file.
        FtpRun run = await RunAsync(Sized + Opened + NotImplemented + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 20) });

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("0123456789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeWithTransferNotOk_DoesNotCheckTheReply()
    {
        // curl -r 3-4 with 451 after the data: exit 0.
        FtpRun run = await RunAsync(
            Sized + Restarting + "150 Opening BINARY mode data connection\r\n451 bad\r\n" + NotImplemented + Bye,
            "3456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(3, 4) });

        Assert.AreEqual(SizeSent + "REST 3\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FromOffsetRangeWithTransferNotOk_FailsWithExit18()
    {
        // curl -r 5- with 451 after the data.
        FtpRun run = await RunAsync(
            Sized + Restarting + "150 Opening BINARY mode data connection\r\n451 bad\r\n" + Bye,
            "56789",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.FromOffset(5) });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "server did not report OK, got 451", 5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeCutShort_FailsWithExit18AndBytesMissing()
    {
        // curl -r 0-14 with SIZE 20 and ten bytes served: no ABOR, no QUIT.
        FtpRun run = await RunAsync(
            InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 20\r\n" + Opened + Bye,
            "0123456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 14) });

        Assert.AreEqual(SizeSent + "RETR f.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing", 10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeCutShort_FailsWithExit18AndBytesRemaining()
    {
        // curl -C 5 with SIZE 20 and five bytes served from the offset.
        FtpRun run = await RunAsync(
            InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 20\r\n" + Restarting + Opened + Bye,
            "56789",
            c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 5 });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with 10 bytes remaining to read", 5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFrom_SendsRestAndWritesTheRest()
    {
        // curl -C 5, and curl -C - -o a file already holding five bytes.
        FtpRun run = await RunAsync(Sized + Restarting + Opened + Bye, "56789", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 5 });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("56789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromZero_SendsNoRest()
    {
        // curl -C - -o a file that does not exist yet.
        FtpRun run = await RunAsync(Sized + Opened + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 0 });

        Assert.AreEqual(SizeSent + "RETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeWithoutSize_SendsRestAnyway()
    {
        // curl -C 5 when SIZE is refused.
        FtpRun run = await RunAsync(
            InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n502 No\r\n" + Restarting + Opened + Bye,
            "56789",
            c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 5 });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAtTheEnd_QuitsWithoutRetrieving()
    {
        // curl -C 10 on the ten-byte file: "File already completely downloaded", exit 0.
        FtpRun run = await RunAsync(Sized + Bye, "", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 10 });

        Assert.AreEqual(SizeSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeAtTheEnd_SendsAborAndQuitsWithoutRetrieving()
    {
        // curl -r 10-12 on the ten-byte file.
        FtpRun run = await RunAsync(Sized + NotImplemented + Bye, "", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(10, 12) });

        Assert.AreEqual(SizeSent + "ABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumePastTheSize_FailsWithExit36()
    {
        // curl -C 20 on the ten-byte file.
        FtpRun run = await RunAsync(Sized + Bye, "", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 20 });

        Assert.AreEqual(SizeSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadDownloadResume, "Offset (20) was beyond file size (10)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixLongerThanTheSize_SendsAborAndFailsWithExit36()
    {
        // curl -r -20 on the ten-byte file.
        FtpRun run = await RunAsync(Sized + NotImplemented + Bye, "", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Suffix(20) });

        Assert.AreEqual(SizeSent + "ABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadDownloadResume, "Offset (-20) was beyond file size (10)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RestRefused_FailsWithExit31AndNoQuit()
    {
        // curl -C 5 with REST answered 502 No.
        FtpRun run = await RunAsync(Sized + "502 No\r\n" + Bye, "", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 5 });

        Assert.AreEqual(SizeSent + "REST 5\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntUseRest, "Could not use REST"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeAndResumeFrom_TheRangeWins()
    {
        FtpRun run = await RunAsync(Sized + Restarting + Opened + Bye, "56789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.FromOffset(5), ResumeFrom = 2 });

        Assert.AreEqual(SizeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeOnADirectoryListing_IsIgnored()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir/",
            InDirectory + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n" + Opened + Bye,
            "a\r\nb\r\n",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 1) });

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("a\r\nb\r\n", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRange_ReportsTheRangeAsTheExpectedSize()
    {
        var progress = new RecordingProgress();

        await RunAsync(Sized + Opened + NotImplemented + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 4), Progress = progress });

        CollectionAssert.AreEqual(new[] { (5L, (long?)5) }, progress.Downloaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadOnAFile_WritesCurlsHeaderLinesWithoutADataConnection()
    {
        // curl -I: MDTM, TYPE I, SIZE, REST 0 and QUIT, no EPSV.
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456\r\n200 Type set\r\n213 10\r\n" + Restarting + Bye);

        Assert.AreEqual(HeadSent, run.Sent);
        Assert.AreEqual(LastModified + "Content-Length: 10\r\nAccept-ranges: bytes\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadOnADirectory_SendsOnlyQuit()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir/",
            InDirectory + Bye,
            "",
            c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true, HeaderOutput = c.Output });

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("550 No", DisplayName = "MDTM refused")]
    [DataRow("213 garbage", DisplayName = "MDTM without a timestamp")]
    [DataRow("213 20261399999999", DisplayName = "MDTM with an impossible timestamp")]
    public async Task ExecuteAsync_HeadWithoutAModificationTime_LeavesOutLastModified(string mdtmReply)
    {
        FtpRun run = await RunHeadAsync(InDirectory + mdtmReply + "\r\n200 Type set\r\n213 10\r\n" + Restarting + Bye);

        Assert.AreEqual(HeadSent, run.Sent);
        Assert.AreEqual("Content-Length: 10\r\nAccept-ranges: bytes\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithFractionalSeconds_KeepsTheWholeSeconds()
    {
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456.789\r\n200 Type set\r\n213 10\r\n" + Restarting + Bye);

        Assert.StartsWith(LastModified, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithoutSize_LeavesOutContentLength()
    {
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456\r\n200 Type set\r\n502 No\r\n" + Restarting + Bye);

        Assert.AreEqual(HeadSent, run.Sent);
        Assert.AreEqual(LastModified + "Accept-ranges: bytes\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithRestRefused_LeavesOutAcceptRanges()
    {
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456\r\n200 Type set\r\n213 10\r\n502 No\r\n" + Bye);

        Assert.AreEqual(HeadSent, run.Sent);
        Assert.AreEqual(LastModified + "Content-Length: 10\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadOnAMissingFile_FailsWithExit78AfterLastModified()
    {
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456\r\n200 Type set\r\n550 No such file\r\n" + Bye);

        Assert.AreEqual(SizeSentForHead + "QUIT\r\n", run.Sent);
        Assert.AreEqual(LastModified, run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithTypeRefused_FailsWithExit17()
    {
        FtpRun run = await RunHeadAsync(InDirectory + "213 20260927123456\r\n504 No\r\n" + Bye);

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nMDTM f.txt\r\nTYPE I\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LastModified, run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntSetType, "Could not set desired mode"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithoutHeaderOutput_WritesNothing()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            InDirectory + "213 20260927123456\r\n200 Type set\r\n213 10\r\n" + Restarting + Bye,
            "",
            c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true });

        Assert.AreEqual(HeadSent, run.Sent);
        Assert.AreEqual("", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWhenTheHeaderOutputRefuses_FailsWithExit23()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            InDirectory + "213 20260927123456\r\n" + Bye,
            "",
            c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true, HeaderOutput = new WriteRefusingStream(new IOException("full")) });

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nMDTM f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WriteError, "client returned ERROR on write of 46 bytes"), run.Result);
    }

    private static Task<FtpRun> RunAsync(string replies, string data, Func<TransferContext, TransferContext> adjust) =>
        FtpRun.ExecuteAsync(Url, replies, data, adjust);

    private static Task<FtpRun> RunHeadAsync(string replies) =>
        FtpRun.ExecuteAsync(Url, replies, "", c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true, HeaderOutput = c.Output });
}
