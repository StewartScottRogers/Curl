using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>-B</c>, the <c>;type=</c> URL suffix, <c>-a</c> and <c>--crlf</c> on
/// <c>ftp://</c> against curl 8.21.0: the commands sent and the data bytes. Every case was
/// recorded from real curl on 2026-09-29 with <c>Record-CurlExchange.ps1 -Ftp -FtpData
/// 'l1\nl2\r\n'</c> against <c>ftp://127.0.0.1:47633/dir/f.txt</c>, uploading the seven
/// bytes <c>a\nb\r\nc\n</c> (BL-633), and is replayed here with the recorder's replies.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTransferTypeTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:47633/dir/f.txt";

    private const string File = "l1\nl2\r\n";

    private const string Upload = "a\nb\r\nc\n";

    /// <summary>The recording server's replies from the greeting through <c>CWD dir</c>.</summary>
    private const string InDirectory = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n";

    /// <summary>Its replies to <c>EPSV</c> and <c>TYPE</c>.</summary>
    private const string Typed = InDirectory + "229 Entering Extended Passive Mode (|||65340|)\r\n200 Type set\r\n";

    private const string Transferred = "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string InDirectorySent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\n";

    /// <summary>What curl sent for an ASCII download of <c>f.txt</c>: no <c>SIZE</c>.</summary>
    private const string AsciiDownloadSent = InDirectorySent + "EPSV\r\nTYPE A\r\nRETR f.txt\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_UseAscii_SendsTypeAAndNoSizeAndCopiesTheBytesUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -B ftp://127.0.0.1:47633/dir/f.txt
        FtpRun run = await DownloadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.UseAscii = true);

        diagnostics.DiffSent(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(File, run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(7), run.Result);
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    [DataRow(";type=a", DisplayName = "lower-case code")]
    [DataRow(";type=A", DisplayName = "upper-case code")]
    public async Task ExecuteAsync_TypeASuffix_SendsTypeAForTheFileWithoutTheSuffix(string suffix)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix", suffix);

        // curl ftp://127.0.0.1:47633/dir/f.txt;type=a
        FtpRun run = await DownloadAsync(diagnostics, Url + suffix, Typed + Transferred + Bye);

        diagnostics.DiffSent(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    [DataRow(";type=i", DisplayName = "binary code")]
    [DataRow(";type=I", DisplayName = "upper-case binary code")]
    [DataRow(";type=x", DisplayName = "unknown code")]
    public async Task ExecuteAsync_UseAsciiWithAnotherTypeCode_SendsTypeIAndSize(string suffix)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix", suffix);

        // curl -B ftp://127.0.0.1:47633/dir/f.txt;type=i
        FtpRun run = await DownloadAsync(diagnostics, Url + suffix, Typed + "213 7\r\n" + Transferred + Bye, context => context.UseAscii = true);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    [DataRow(";type=d", DisplayName = "lower-case code")]
    [DataRow(";type=D", DisplayName = "upper-case code")]
    public async Task ExecuteAsync_TypeDSuffixOnAFile_ListsNamesWithNlst(string suffix)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("suffix", suffix);

        // curl ftp://127.0.0.1:47633/dir/f.txt;type=d
        FtpRun run = await DownloadAsync(diagnostics, Url + suffix, Typed + Transferred + Bye);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyWithTypeASuffix_ListsNamesWithNlst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -l ftp://127.0.0.1:47633/dir/f.txt;type=a
        FtpRun run = await DownloadAsync(diagnostics, Url + ";type=a", Typed + Transferred + Bye, context => context.ListOnly = true);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeASuffixOnADirectory_ListsWithList()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl ftp://127.0.0.1:47633/dir/;type=a
        FtpRun run = await DownloadAsync(diagnostics, "ftp://127.0.0.1:47633/dir/;type=a", Typed + Transferred + Bye);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("f.txt;TYPE=A", "f.txt;TYPE=A", DisplayName = "upper-case key")]
    [DataRow("f.txt;type=ab", "f.txt;type=ab", DisplayName = "two characters")]
    [DataRow("f.txt;type=", "f.txt;type=", DisplayName = "no character")]
    [DataRow("f.txt%3Btype=a", "f.txt;type=a", DisplayName = "encoded semicolon")]
    public async Task ExecuteAsync_SuffixThatIsNoTypeCode_StaysInTheFileName(string name, string sentName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file name", name);
        diagnostics.Arrange("name expected on the wire", sentName);

        // curl ftp://127.0.0.1:47633/dir/f.txt;TYPE=A
        FtpRun run = await DownloadAsync(diagnostics, "ftp://127.0.0.1:47633/dir/" + name, Typed + "213 7\r\n" + Transferred + Bye);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE " + sentName + "\r\nRETR " + sentName + "\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE " + sentName + "\r\nRETR " + sentName + "\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeCodeInADirectorySegment_StaysInThatDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl ftp://127.0.0.1:47633/dir;type=a/f.txt
        FtpRun run = await DownloadAsync(diagnostics, "ftp://127.0.0.1:47633/dir;type=a/f.txt", Typed + "213 7\r\n" + Transferred + Bye);

        diagnostics.DiffSent(
            "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir;type=a\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n",
            run.Sent);
        Assert.AreEqual(
            "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir;type=a\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n",
            run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UseAsciiWithResume_SendsNoRestAndReadsTheWholeFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -B -C 3
        FtpRun run = await DownloadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.UseAscii = true, resumeFrom: 3);

        diagnostics.DiffSent(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_UseAsciiWithBoundedRange_ReadsTheRangesLengthFromTheStartThenSendsAbor()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -B -r 1-3: no REST, three bytes from the start, then ABOR.
        FtpRun run = await DownloadAsync(
            diagnostics,
            Url,
            Typed + Transferred + "502 Command not implemented\r\n" + Bye,
            context => context.UseAscii = true,
            range: ByteRange.Bounded(1, 3));

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("l1\n", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UseAsciiWithOpenRange_ReadsTheWholeFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -B -r 2-
        FtpRun run = await DownloadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.UseAscii = true, range: ByteRange.FromOffset(2));

        diagnostics.DiffSent(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(AsciiDownloadSent, run.Sent);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_UseAsciiHead_SendsTypeAThenSizeAndRest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -B -I
        string replies = InDirectory + "213 20260927123456\r\n200 Type set\r\n213 7\r\n350 Restarting\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies, "");
        diagnostics.Arrange("options", "-B -I");
        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "", c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true, HeaderOutput = c.Output, UseAscii = true });
        diagnostics.ActRun(run);

        diagnostics.DiffSent(InDirectorySent + "MDTM f.txt\r\nTYPE A\r\nSIZE f.txt\r\nREST 0\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "MDTM f.txt\r\nTYPE A\r\nSIZE f.txt\r\nREST 0\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(
            "Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\nContent-Length: 7\r\nAccept-ranges: bytes\r\n",
            run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_UseAsciiUpload_SendsTypeAAndTheBytesUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -T up.txt -B
        FtpRun run = await UploadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.UseAscii = true);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        diagnostics.Assert("result", TransferResult.Success(7), run.Result);
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToATypeASuffix_SendsTypeAAndStoresWithoutTheSuffix()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -T up.txt ftp://127.0.0.1:47633/dir/f.txt;type=a
        FtpRun run = await UploadAsync(diagnostics, Url + ";type=a", Typed + Transferred + Bye);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE A\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_Append_UploadsWithAppeAndTheBytesUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -T up.txt -a
        FtpRun run = await UploadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.Append = true);

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE I\r\nAPPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE I\r\nAPPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        diagnostics.Assert("result", TransferResult.Success(7), run.Result);
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AppendResumingAtTheRemoteSize_SendsSizeThenQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -T up.txt -a -C -: the server already holds all seven bytes.
        FtpRun run = await UploadAsync(
            diagnostics,
            Url,
            Typed + "213 7\r\n" + Bye,
            context =>
            {
                context.Append = true;
                context.ResumeUploadFromUnknownOffset = true;
            });

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        diagnostics.Assert("result", TransferResult.Success(0), run.Result);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConvertLineEndings_SendsAndCountsTheConvertedBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -T up.txt --crlf: a\nb\r\nc\n is sent as the nine bytes a\r\nb\r\nc\r\n.
        var events = new RecordingTransferEvents();
        var progress = new RecordingProgress();
        FtpRun run = await UploadAsync(
            diagnostics,
            Url,
            Typed + Transferred + Bye,
            context =>
            {
                context.ConvertLineEndings = true;
                context.Events = events;
                context.Progress = progress;
            });

        diagnostics.DiffSent(InDirectorySent + "EPSV\r\nTYPE I\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE I\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("a\r\nb\r\nc\r\n", Encoding.Latin1.GetString(run.Data.Sent));
        diagnostics.Assert("result", TransferResult.Success(9), run.Result);
        Assert.AreEqual(TransferResult.Success(9), run.Result);
        Assert.Contains("* upload completely sent off: 9 bytes", events.Transcript);
        Assert.AreEqual(9L, progress.Uploaded[^1].Item1);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConvertLineEndingsOverSeveralReads_KeepsACarriageReturnEndingOneReadPairedWithTheNext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // 16383 bytes, then \r\n: the carriage return ends the first 16384-byte read and the
        // line feed starts the next, so nothing is inserted between them.
        string content = new string('x', 16383) + "\r\n" + "y\n";
        FtpRun run = await UploadAsync(diagnostics, Url, Typed + Transferred + Bye, context => context.ConvertLineEndings = true, content);

        diagnostics.Diff("uploaded bytes", new string('x', 16383) + "\r\ny\r\n", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(new string('x', 16383) + "\r\ny\r\n",Encoding.Latin1.GetString(run.Data.Sent));
    }

    private static async Task<FtpRun> DownloadAsync(
        TestDiagnostics diagnostics,
        string url,
        string replies,
        Action<MutableFtpContext>? adjust = null,
        long? resumeFrom = null,
        ByteRange? range = null)
    {
        diagnostics.ArrangeFtp(url, replies, File);
        diagnostics.Arrange("resume from", resumeFrom);
        diagnostics.Arrange("range", range);
        FtpRun run = await FtpRun.ExecuteAsync(url, replies, File, c => MutableFtpContext.Build(c, null, adjust, resumeFrom, range));
        diagnostics.ActRun(run);
        return run;
    }

    private static async Task<FtpRun> UploadAsync(TestDiagnostics diagnostics, string url, string replies, Action<MutableFtpContext>? adjust = null, string content = Upload)
    {
        diagnostics.ArrangeFtp(url, replies);
        diagnostics.Arrange("upload bytes", content.Length);
        FtpRun run = await FtpRun.ExecuteAsync(url, replies, "", c => MutableFtpContext.Build(c, new MemoryStream(Encoding.Latin1.GetBytes(content)), adjust, null, null));
        diagnostics.ActRun(run);
        return run;
    }

    /// <summary>The options these tests set, gathered before the context is built.</summary>
    private sealed class MutableFtpContext
    {
        public bool UseAscii { get; set; }

        public bool ListOnly { get; set; }

        public bool Append { get; set; }

        public bool ConvertLineEndings { get; set; }

        public bool ResumeUploadFromUnknownOffset { get; set; }

        public ITransferEvents Events { get; set; } = NoTransferEvents.Instance;

        public ITransferProgress Progress { get; set; } = NoTransferProgress.Instance;

        public static TransferContext Build(TransferContext c, Stream? upload, Action<MutableFtpContext>? adjust, long? resumeFrom, ByteRange? range)
        {
            var mutable = new MutableFtpContext();
            adjust?.Invoke(mutable);
            return new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                Upload = upload,
                ResumeFrom = resumeFrom,
                Range = range,
                UseAscii = mutable.UseAscii,
                ListOnly = mutable.ListOnly,
                Append = mutable.Append,
                ConvertLineEndings = mutable.ConvertLineEndings,
                ResumeUploadFromUnknownOffset = mutable.ResumeUploadFromUnknownOffset,
                Events = mutable.Events,
                Progress = mutable.Progress,
            };
        }
    }
}
