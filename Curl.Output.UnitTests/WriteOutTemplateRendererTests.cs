using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTemplateRenderer"/> to the bytes curl 8.21.0 (mingw, Schannel)
/// wrote for the same templates on 2026-09-26; the commands are in BL-224's Notes.
/// </summary>
[TestClass]
public sealed class WriteOutTemplateRendererTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string UnknownNoSuch = "curl: unknown --write-out variable: 'nosuch'";

    [TestMethod]
    public async Task RenderAsync_LiteralsEscapesAndUnknownPercent_MatchCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "a%%b\n|\r|\t|\\|\x|%{http_code}|%x|%{|end" wrote "a%b\r\n|\r|\t|\\|\x|000|%x|%{|end".
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["http_code"] = "000";

        await harness.RenderAsync("a%%b\\n|\\r|\\t|\\\\|\\x|%{http_code}|%x|%{|end", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "a%b\r\n|\r|\t|\\\\|\\x|000|%x|%{|end", harness.StandardOutputText);
        Assert.AreEqual("a%b\r\n|\r|\t|\\\\|\\x|000|%x|%{|end", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", string.Empty, harness.StandardErrorText);
        Assert.AreEqual(string.Empty, harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_TrailingPercentAndBackslash_AreWrittenAsTheyStand()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "A...|\q|%" ended "|\q|%"; curl -w Z\ wrote "Z\".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("\\q|%|Z\\", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "\\q|%|Z\\", harness.StandardOutputText);
        Assert.AreEqual("\\q|%|Z\\", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_LineFeedWithoutTranslation_StaysALineFeed()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: false);
        harness.Variables.Values["v"] = "x\ny";

        await harness.RenderAsync("a\\n%{v}", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "a\nx\ny", harness.StandardOutputText);
        Assert.AreEqual("a\nx\ny", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableValueWithLineFeed_IsTranslatedToo()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["v"] = "x\ny";

        await harness.RenderAsync("%{v}", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "x\r\ny", harness.StandardOutputText);
        Assert.AreEqual("x\r\ny", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnknownVariable_WritesTheWarningAndNothingElse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "x%{nosuch}y%{" wrote "xy%{" and the warning line on standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("x%{nosuch}y%{", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "xy%{", harness.StandardOutputText);
        Assert.AreEqual("xy%{", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", UnknownNoSuch + "\r\n", harness.StandardErrorText);
        Assert.AreEqual(UnknownNoSuch + "\r\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNameOf24Bytes_StopsTheOutputSilently()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "a%{abcdefghijklmnopqrstuvw}b%{abcdefghijklmnopqrstuvwx}c\n" file:///C:/Windows/win.ini
        // wrote "ab" and only the 23-byte name's warning (BL-1301).
        Harness harness = new(writesLineFeedAsCrLf: false);

        await harness.RenderAsync("a%{abcdefghijklmnopqrstuvw}b%{abcdefghijklmnopqrstuvwx}c\\n", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "ab", harness.StandardOutputText);
        Assert.AreEqual("ab", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", "curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'\n", harness.StandardErrorText);
        Assert.AreEqual("curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNameLengths_StopOnlyFrom24Bytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: false);
        harness.Variables.Values["http_code"] = "200";

        await harness.RenderAsync("%{http_code}|%{abcdefghijklmnopqrstuvw}|%{" + new string('é', 12) + "}|%{http_code}", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "200||", harness.StandardOutputText);
        Assert.AreEqual("200||", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", "curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'\n", harness.StandardErrorText);
        Assert.AreEqual("curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_LongNameAfterSwitches_FlushesPendingTextAndClosesTheFile()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: false);

        await harness.RenderAsync("O%output{f.txt}F%{stderr}E%{abcdefghijklmnopqrstuvwx}Z", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "O", harness.StandardOutputText);
        Assert.AreEqual("O", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", "E", harness.StandardErrorText);
        Assert.AreEqual("E", harness.StandardErrorText);
        diagnostics.Diff("file content", "F", harness.Files.ContentOf("f.txt"));
        Assert.AreEqual("F", harness.Files.ContentOf("f.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_LongNameWhileWritingToAFile_FlushesAndClosesTheFile()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: false);

        await harness.RenderAsync("%output{g.txt}G%{abcdefghijklmnopqrstuvwx}Z", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", string.Empty, harness.StandardOutputText);
        Assert.AreEqual(string.Empty, harness.StandardOutputText);
        diagnostics.Diff("file content", "G", harness.Files.ContentOf("g.txt"));
        Assert.AreEqual("G", harness.Files.ContentOf("g.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNames_AreCaseSensitiveAndUntrimmed()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%{HTTP_CODE}|%{ http_code}|%{}|%" wrote "|||%" and three warnings.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["http_code"] = "200";

        await harness.RenderAsync("%{HTTP_CODE}|%{ http_code}|%{}|%", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "|||%", harness.StandardOutputText);
        Assert.AreEqual("|||%", harness.StandardOutputText);
        const string expectedWarnings = "curl: unknown --write-out variable: 'HTTP_CODE'\r\n"
            + "curl: unknown --write-out variable: ' http_code'\r\n"
            + "curl: unknown --write-out variable: ''\r\n";
        diagnostics.Diff("harness.StandardErrorText", expectedWarnings, harness.StandardErrorText);
        Assert.AreEqual(
            "curl: unknown --write-out variable: 'HTTP_CODE'\r\n"
            + "curl: unknown --write-out variable: ' http_code'\r\n"
            + "curl: unknown --write-out variable: ''\r\n",
            harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_StandardErrorThenStandardOutput_SwitchesTheTarget()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "x%{nosuch}y%{stderr}E\n%{stdout}O..." wrote "xy" then "O" on standard
        // output and the warning then "E\r\n" on standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("x%{nosuch}y%{stderr}E\\n%{stdout}O", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "xyO", harness.StandardOutputText);
        Assert.AreEqual("xyO", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", UnknownNoSuch + "\r\nE\r\n", harness.StandardErrorText);
        Assert.AreEqual(UnknownNoSuch + "\r\nE\r\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_WarningWhileOnStandardError_KeepsTheOrder()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%output{o9.txt}X\n%{stderr}Y%output{o9b.txt}%{nosuch}Z" wrote "X\r\n" to
        // o9.txt, "Y" then the warning to standard error, and "Z" to o9b.txt.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("%output{o9.txt}X\\n%{stderr}Y%output{o9b.txt}%{nosuch}Z", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", string.Empty, harness.StandardOutputText);
        Assert.AreEqual(string.Empty, harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", "Y" + UnknownNoSuch + "\r\n", harness.StandardErrorText);
        Assert.AreEqual("Y" + UnknownNoSuch + "\r\n", harness.StandardErrorText);
        diagnostics.Diff("file content", "X\r\n", harness.Files.ContentOf("o9.txt"));
        Assert.AreEqual("X\r\n", harness.Files.ContentOf("o9.txt"));
        diagnostics.Diff("file content", "Z", harness.Files.ContentOf("o9b.txt"));
        Assert.AreEqual("Z", harness.Files.ContentOf("o9b.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_OutputAndAppend_WriteToTheFiles()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "...%{stdout}O%output{o3.txt}F\nG%output{>>o3.txt}H\n%{stdout}Z" wrote
        // "F\r\nGH\r\n" to o3.txt and "Z" to standard output.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("O%output{o3.txt}F\\nG%output{>>o3.txt}H\\n%{stdout}Z", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "OZ", harness.StandardOutputText);
        Assert.AreEqual("OZ", harness.StandardOutputText);
        diagnostics.Diff("file content", "F\r\nGH\r\n", harness.Files.ContentOf("o3.txt"));
        Assert.AreEqual("F\r\nGH\r\n", harness.Files.ContentOf("o3.txt"));
        diagnostics.Diff("harness.Files.OpenLog", "o3.txt:truncate,o3.txt:append", harness.Files.OpenLog);
        Assert.AreEqual("o3.txt:truncate,o3.txt:append", harness.Files.OpenLog);
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_UnopenableAndUnclosedOutput_StayOnTheCurrentFile()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%output{o4.txt}A%output{>>o4.txt}B%output{o4.txt}C%output{}D%output{nodir\x\y.txt}E%output{Q"
        // left "CDE%output{Q" in o4.txt and nothing on standard output.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Files.Unopenable.Add(string.Empty);
        harness.Files.Unopenable.Add("nodir\\x\\y.txt");

        await harness.RenderAsync("%output{o4.txt}A%output{>>o4.txt}B%output{o4.txt}C%output{}D%output{nodir\\x\\y.txt}E%output{Q", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", string.Empty, harness.StandardOutputText);
        Assert.AreEqual(string.Empty, harness.StandardOutputText);
        diagnostics.Diff("file content", "CDE%output{Q", harness.Files.ContentOf("o4.txt"));
        Assert.AreEqual("CDE%output{Q", harness.Files.ContentOf("o4.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedAppendOutput_DropsTheAppendMarker()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl's own parser steps past ">>" before looking for the closing brace.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("A%output{>>Q", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "A%output{Q", harness.StandardOutputText);
        Assert.AreEqual("A%output{Q", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_FileNameOf512Bytes_IsNotOpened()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%output{<600 a's>}L" wrote "L" to standard output and created no file.
        Harness harness = new(writesLineFeedAsCrLf: true);
        string longest = new('a', 511);
        string tooLong = new('a', 512);

        await harness.RenderAsync("%output{" + tooLong + "}L%output{" + longest + "}M", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "L", harness.StandardOutputText);
        Assert.AreEqual("L", harness.StandardOutputText);
        diagnostics.Diff("harness.Files.OpenLog", longest + ":truncate", harness.Files.OpenLog);
        Assert.AreEqual(longest + ":truncate", harness.Files.OpenLog);
        diagnostics.Diff("file content", "M", harness.Files.ContentOf(longest));
        Assert.AreEqual("M", harness.Files.ContentOf(longest));
    }

    [TestMethod]
    public async Task RenderAsync_Header_WritesTheFirstValueOrNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Against a server sending X-Dup: one, X-Dup: two, X-Lf: "a b  " and Content-Length: 0,
        // curl -w "[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}][%header{}][%header{content-length}]"
        // wrote "[one][one][a b][][][0]"; the source does the matching and trimming.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Headers["x-dup"] = "one";
        harness.Variables.Headers["x-lf"] = "a b";
        harness.Variables.Headers["content-length"] = "0";

        await harness.RenderAsync("[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}][%header{}][%header{content-length}]", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "[one][one][a b][][][0]", harness.StandardOutputText);
        Assert.AreEqual("[one][one][a b][][][0]", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedHeader_IsWrittenAsItStands()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%header{content-length}|%header{x}|%header{Q" on file:// wrote "||%header{Q".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("%header{content-length}|%header{x}|%header{Q", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "||%header{Q", harness.StandardOutputText);
        Assert.AreEqual("||%header{Q", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_HeaderNameOf256Bytes_IsNotLookedUp()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: true);
        string longest = new('h', 255);
        string tooLong = new('h', 256);
        harness.Variables.Headers[longest] = "L";
        harness.Variables.Headers[tooLong] = "T";

        await harness.RenderAsync("%header{" + longest + "}%header{" + tooLong + "}", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "L", harness.StandardOutputText);
        Assert.AreEqual("L", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_NonAsciiText_IsWrittenAsUtf8()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("é%x😀", diagnostics);

        byte[] expected = Encoding.UTF8.GetBytes("é%x😀");
        byte[] actual = harness.StandardOutput.ToArray();
        diagnostics.Bytes("standard output", actual);
        diagnostics.Diff("standard output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task RenderAsync_FailingWrite_StillClosesTheOpenedFile()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        RecordingFileOpener files = new();
        WriteOutTemplateRenderer renderer = new(files, writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using FailingStream standardError = new();

        diagnostics.Arrange("template", "%output{f.txt}X%{nosuch}");

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => renderer.RenderAsync("%output{f.txt}X%{nosuch}", new DictionaryVariableSource(), standardOutput, standardError));

        diagnostics.Act("throws", exception.GetType().Name + ": " + exception.Message);
        diagnostics.Assert("exception type", nameof(IOException), exception.GetType().Name);
        diagnostics.Assert("all files closed", true, files.AllClosed);
        diagnostics.Diff("file content", "X", files.ContentOf("f.txt"));
        Assert.IsTrue(files.AllClosed);
        Assert.AreEqual("X", files.ContentOf("f.txt"));
    }

    [TestMethod]
    public async Task RenderAsync_StreamsThatCompleteLater_RenderTheSameBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        WriteOutTemplateRenderer renderer = new(new YieldingFileOpener(), writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);
        using YieldingStream standardOutput = new();
        using YieldingStream standardError = new();

        diagnostics.Arrange("template", "a%{stderr}b%{stdout}c%output{f.txt}d");

        await renderer.RenderAsync("a%{stderr}b%{stdout}c%output{f.txt}d", new DictionaryVariableSource(), standardOutput, standardError);

        diagnostics.Act("standard output", Encoding.UTF8.GetString(standardOutput.ToArray()));
        diagnostics.Act("standard error", Encoding.UTF8.GetString(standardError.ToArray()));
        diagnostics.Diff("standard output", "ac", Encoding.UTF8.GetString(standardOutput.ToArray()));
        diagnostics.Diff("standard error", "b", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual("ac", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual("b", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RenderAsync_PreviousFileFailsToClose_StillClosesTheNewFile()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Files.FailingClose.Add("a.txt");

        diagnostics.Arrange("template", "%output{a.txt}A%output{b.txt}B");
        diagnostics.Arrange("failing close", "a.txt");

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(() => harness.RenderAsync("%output{a.txt}A%output{b.txt}B"));

        diagnostics.Act("throws", exception.GetType().Name + ": " + exception.Message);
        diagnostics.Assert("exception type", nameof(IOException), exception.GetType().Name);
        diagnostics.Assert("all files closed", true, harness.Files.AllClosed);

        Assert.IsTrue(harness.Files.AllClosed);
        diagnostics.Diff("file content", "A", harness.Files.ContentOf("a.txt"));
        Assert.AreEqual("A", harness.Files.ContentOf("a.txt"));
    }

    private sealed class YieldingStream : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            await Task.Yield();
            await base.DisposeAsync();
        }
    }

    private sealed class YieldingFileOpener : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = new YieldingStream();
            return true;
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("standard error is closed");
        }
    }

    [TestMethod]
    public void UnknownVariableWarning_Name_IsCurlsLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("name", "nosuch");

        string warning = WriteOutTemplateRenderer.UnknownVariableWarning("nosuch");

        diagnostics.Act("warning", warning);
        diagnostics.Diff("warning", UnknownNoSuch, warning);
        Assert.AreEqual(UnknownNoSuch, warning);
    }

    [TestMethod]
    public void UnknownVariableWarning_NullName_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("name", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTemplateRenderer.UnknownVariableWarning(null!));

        ThrowLines(diagnostics, exception);
    }

    [TestMethod]
    public async Task RenderAsync_OnErrorAfterSuccess_StopsThere()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "a\n%{onerror}b%{stderr}c" file:///c:/Windows/win.ini wrote "a\r\n" and nothing to standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("a\\n%{onerror}b%{stderr}c", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "a\r\n", harness.StandardOutputText);
        Assert.AreEqual("a\r\n", harness.StandardOutputText);
        diagnostics.Diff("harness.StandardErrorText", string.Empty, harness.StandardErrorText);
        Assert.AreEqual(string.Empty, harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_OnErrorAfterFailure_RendersNothingAndCarriesOn()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "a%{onerror}b%{onerror}c" file:///c:/nonexist wrote "abc" and exited 37.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.TransferFailed = true;

        await harness.RenderAsync("a%{onerror}b%{onerror}c", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "abc", harness.StandardOutputText);
        Assert.AreEqual("abc", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_Time_RendersTheFormattedTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "[%time{%Y}]Q" wrote "[2026]Q"; %time{%Y]%{url} closes at the url's brace and prints nothing.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("[%time{%Y-%m-%dT%H:%M:%S.%f%z}]Q|%time{%Y]%{url}|", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "[2026-09-27T03:30:08.545957+0000]Q||", harness.StandardOutputText);
        Assert.AreEqual("[2026-09-27T03:30:08.545957+0000]Q||", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_TimeInTheGlibcDialect_RendersWhatLinuxCurlPrints()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Linux curl 8.21.0 prints glibc's %F and %T, which the Windows C runtime rejects.
        Harness harness = new(writesLineFeedAsCrLf: false, WriteOutTimeDialect.Glibc);

        await harness.RenderAsync("[%time{%F %T.%f}]", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "[2026-09-27 03:30:08.545957]", harness.StandardOutputText);
        Assert.AreEqual("[2026-09-27 03:30:08.545957]", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedTime_IsWrittenAsItStands()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "[%time{%Y]" wrote "[%time{%Y]".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("[%time{%Y]", diagnostics);

        diagnostics.Diff("harness.StandardOutputText", "[%time{%Y]", harness.StandardOutputText);
        Assert.AreEqual("[%time{%Y]", harness.StandardOutputText);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("time provider", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WriteOutTemplateRenderer(new RecordingFileOpener(), writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, null!));

        ThrowLines(diagnostics, exception);
    }

    [TestMethod]
    public void Constructor_NullFileOpener_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file opener", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WriteOutTemplateRenderer(null!, writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System));

        ThrowLines(diagnostics, exception);
    }

    [TestMethod]
    public async Task RenderAsync_NullArgument_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        WriteOutTemplateRenderer renderer = new(new RecordingFileOpener(), writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);
        DictionaryVariableSource variables = new();
        using MemoryStream stream = new();

        diagnostics.Arrange("null arguments", "template, variables, standard output, standard error, one at a time");

        ArgumentNullException noTemplate = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync(null!, variables, stream, stream));
        ArgumentNullException noVariables = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", null!, stream, stream));
        ArgumentNullException noOutput = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", variables, null!, stream));
        ArgumentNullException noError = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", variables, stream, null!));

        ThrowLines(diagnostics, noTemplate);
        ThrowLines(diagnostics, noVariables);
        ThrowLines(diagnostics, noOutput);
        ThrowLines(diagnostics, noError);
    }

    private static void ThrowLines(TestDiagnostics diagnostics, ArgumentNullException exception)
    {
        diagnostics.Act("throws", exception.GetType().Name + ": " + exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private sealed class Harness(bool writesLineFeedAsCrLf, WriteOutTimeDialect timeDialect = WriteOutTimeDialect.WindowsCRuntime)
    {
        public WriteOutTimeDialect TimeDialect { get; } = timeDialect;

        public DictionaryVariableSource Variables { get; } = new();

        public RecordingFileOpener Files { get; } = new();

        public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 27, 3, 30, 8, TimeSpan.Zero).AddTicks(5_459_570));

        public MemoryStream StandardOutput { get; } = new();

        public MemoryStream StandardError { get; } = new();

        public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput.ToArray());

        public string StandardErrorText => Encoding.UTF8.GetString(StandardError.ToArray());

        public async Task RenderAsync(string template, TestDiagnostics diagnostics)
        {
            diagnostics.Arrange("template", template);
            diagnostics.Arrange("line feed as CR LF", writesLineFeedAsCrLf);
            await RenderAsync(template);
            diagnostics.Act("standard output", StandardOutputText);
            diagnostics.Act("standard error", StandardErrorText);
        }

        public Task RenderAsync(string template)
        {
            WriteOutTemplateRenderer renderer = new(Files, writesLineFeedAsCrLf, TimeDialect, Clock);
            return renderer.RenderAsync(template, Variables, StandardOutput, StandardError);
        }
    }

    private sealed class DictionaryVariableSource : IWriteOutVariableSource
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool TransferFailed { get; set; }

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            return Values.TryGetValue(name, out text);
        }

        public string? FindFirstHeaderValue(string name)
        {
            return Headers.GetValueOrDefault(name);
        }
    }

    private sealed class RecordingFileOpener : IWriteOutFileOpener
    {
        private readonly Dictionary<string, byte[]> contents = new(StringComparer.Ordinal);
        private readonly List<string> opens = [];
        private int openCount;

        public HashSet<string> Unopenable { get; } = new(StringComparer.Ordinal);

        public HashSet<string> FailingClose { get; } = new(StringComparer.Ordinal);

        public string OpenLog => string.Join(",", opens);

        public bool AllClosed => openCount == 0;

        public string ContentOf(string path)
        {
            return Encoding.UTF8.GetString(contents[path]);
        }

        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            if (Unopenable.Contains(path))
            {
                stream = null;
                return false;
            }

            opens.Add(path + (append ? ":append" : ":truncate"));
            byte[] existing = append ? contents.GetValueOrDefault(path, []) : [];
            contents[path] = existing;
            openCount++;
            stream = new RecordingFileStream(this, path, existing);
            return true;
        }

        /// <summary>
        /// Like a real file, an open stream's writes reach the file only when it is flushed or
        /// closed, so a second handle opened before that does not see them.
        /// </summary>
        private sealed class RecordingFileStream : MemoryStream
        {
            private readonly RecordingFileOpener owner;
            private readonly string path;
            private bool unflushed;
            private bool closed;

            public RecordingFileStream(RecordingFileOpener owner, string path, byte[] existing)
            {
                this.owner = owner;
                this.path = path;
                Write(existing);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                unflushed = true;
                return base.WriteAsync(buffer, cancellationToken);
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                CommitIfUnflushed();
                return Task.CompletedTask;
            }

            protected override void Dispose(bool disposing)
            {
                if (!closed)
                {
                    closed = true;
                    CommitIfUnflushed();
                    owner.openCount--;
                    if (owner.FailingClose.Contains(path))
                    {
                        throw new IOException("the disk is full");
                    }
                }

                base.Dispose(disposing);
            }

            private void CommitIfUnflushed()
            {
                if (unflushed)
                {
                    unflushed = false;
                    owner.contents[path] = ToArray();
                }
            }
        }
    }
}
