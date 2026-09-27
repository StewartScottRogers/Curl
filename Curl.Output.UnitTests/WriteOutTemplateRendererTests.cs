using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTemplateRenderer"/> to the bytes curl 8.21.0 (mingw, Schannel)
/// wrote for the same templates on 2026-09-26; the commands are in BL-224's Notes.
/// </summary>
[TestClass]
public sealed class WriteOutTemplateRendererTests
{
    private const string UnknownNoSuch = "curl: unknown --write-out variable: 'nosuch'";

    [TestMethod]
    public async Task RenderAsync_LiteralsEscapesAndUnknownPercent_MatchCurl()
    {
        // curl -w "a%%b\n|\r|\t|\\|\x|%{http_code}|%x|%{|end" wrote "a%b\r\n|\r|\t|\\|\x|000|%x|%{|end".
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["http_code"] = "000";

        await harness.RenderAsync("a%%b\\n|\\r|\\t|\\\\|\\x|%{http_code}|%x|%{|end");

        Assert.AreEqual("a%b\r\n|\r|\t|\\\\|\\x|000|%x|%{|end", harness.StandardOutputText);
        Assert.AreEqual(string.Empty, harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_TrailingPercentAndBackslash_AreWrittenAsTheyStand()
    {
        // curl -w "A...|\q|%" ended "|\q|%"; curl -w Z\ wrote "Z\".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("\\q|%|Z\\");

        Assert.AreEqual("\\q|%|Z\\", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_LineFeedWithoutTranslation_StaysALineFeed()
    {
        Harness harness = new(writesLineFeedAsCrLf: false);
        harness.Variables.Values["v"] = "x\ny";

        await harness.RenderAsync("a\\n%{v}");

        Assert.AreEqual("a\nx\ny", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableValueWithLineFeed_IsTranslatedToo()
    {
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["v"] = "x\ny";

        await harness.RenderAsync("%{v}");

        Assert.AreEqual("x\r\ny", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnknownVariable_WritesTheWarningAndNothingElse()
    {
        // curl -w "x%{nosuch}y%{" wrote "xy%{" and the warning line on standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("x%{nosuch}y%{");

        Assert.AreEqual("xy%{", harness.StandardOutputText);
        Assert.AreEqual(UnknownNoSuch + "\r\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNames_AreCaseSensitiveAndUntrimmed()
    {
        // curl -w "%{HTTP_CODE}|%{ http_code}|%{}|%" wrote "|||%" and three warnings.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Values["http_code"] = "200";

        await harness.RenderAsync("%{HTTP_CODE}|%{ http_code}|%{}|%");

        Assert.AreEqual("|||%", harness.StandardOutputText);
        Assert.AreEqual(
            "curl: unknown --write-out variable: 'HTTP_CODE'\r\n"
            + "curl: unknown --write-out variable: ' http_code'\r\n"
            + "curl: unknown --write-out variable: ''\r\n",
            harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_StandardErrorThenStandardOutput_SwitchesTheTarget()
    {
        // curl -w "x%{nosuch}y%{stderr}E\n%{stdout}O..." wrote "xy" then "O" on standard
        // output and the warning then "E\r\n" on standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("x%{nosuch}y%{stderr}E\\n%{stdout}O");

        Assert.AreEqual("xyO", harness.StandardOutputText);
        Assert.AreEqual(UnknownNoSuch + "\r\nE\r\n", harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_WarningWhileOnStandardError_KeepsTheOrder()
    {
        // curl -w "%output{o9.txt}X\n%{stderr}Y%output{o9b.txt}%{nosuch}Z" wrote "X\r\n" to
        // o9.txt, "Y" then the warning to standard error, and "Z" to o9b.txt.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("%output{o9.txt}X\\n%{stderr}Y%output{o9b.txt}%{nosuch}Z");

        Assert.AreEqual(string.Empty, harness.StandardOutputText);
        Assert.AreEqual("Y" + UnknownNoSuch + "\r\n", harness.StandardErrorText);
        Assert.AreEqual("X\r\n", harness.Files.ContentOf("o9.txt"));
        Assert.AreEqual("Z", harness.Files.ContentOf("o9b.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_OutputAndAppend_WriteToTheFiles()
    {
        // curl -w "...%{stdout}O%output{o3.txt}F\nG%output{>>o3.txt}H\n%{stdout}Z" wrote
        // "F\r\nGH\r\n" to o3.txt and "Z" to standard output.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("O%output{o3.txt}F\\nG%output{>>o3.txt}H\\n%{stdout}Z");

        Assert.AreEqual("OZ", harness.StandardOutputText);
        Assert.AreEqual("F\r\nGH\r\n", harness.Files.ContentOf("o3.txt"));
        Assert.AreEqual("o3.txt:truncate,o3.txt:append", harness.Files.OpenLog);
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_UnopenableAndUnclosedOutput_StayOnTheCurrentFile()
    {
        // curl -w "%output{o4.txt}A%output{>>o4.txt}B%output{o4.txt}C%output{}D%output{nodir\x\y.txt}E%output{Q"
        // left "CDE%output{Q" in o4.txt and nothing on standard output.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Files.Unopenable.Add(string.Empty);
        harness.Files.Unopenable.Add("nodir\\x\\y.txt");

        await harness.RenderAsync("%output{o4.txt}A%output{>>o4.txt}B%output{o4.txt}C%output{}D%output{nodir\\x\\y.txt}E%output{Q");

        Assert.AreEqual(string.Empty, harness.StandardOutputText);
        Assert.AreEqual("CDE%output{Q", harness.Files.ContentOf("o4.txt"));
        Assert.IsTrue(harness.Files.AllClosed);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedAppendOutput_DropsTheAppendMarker()
    {
        // curl's own parser steps past ">>" before looking for the closing brace.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("A%output{>>Q");

        Assert.AreEqual("A%output{Q", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_FileNameOf512Bytes_IsNotOpened()
    {
        // curl -w "%output{<600 a's>}L" wrote "L" to standard output and created no file.
        Harness harness = new(writesLineFeedAsCrLf: true);
        string longest = new('a', 511);
        string tooLong = new('a', 512);

        await harness.RenderAsync("%output{" + tooLong + "}L%output{" + longest + "}M");

        Assert.AreEqual("L", harness.StandardOutputText);
        Assert.AreEqual(longest + ":truncate", harness.Files.OpenLog);
        Assert.AreEqual("M", harness.Files.ContentOf(longest));
    }

    [TestMethod]
    public async Task RenderAsync_Header_WritesTheFirstValueOrNothing()
    {
        // Against a server sending X-Dup: one, X-Dup: two, X-Lf: "a b  " and Content-Length: 0,
        // curl -w "[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}][%header{}][%header{content-length}]"
        // wrote "[one][one][a b][][][0]"; the source does the matching and trimming.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.Headers["x-dup"] = "one";
        harness.Variables.Headers["x-lf"] = "a b";
        harness.Variables.Headers["content-length"] = "0";

        await harness.RenderAsync("[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}][%header{}][%header{content-length}]");

        Assert.AreEqual("[one][one][a b][][][0]", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedHeader_IsWrittenAsItStands()
    {
        // curl -w "%header{content-length}|%header{x}|%header{Q" on file:// wrote "||%header{Q".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("%header{content-length}|%header{x}|%header{Q");

        Assert.AreEqual("||%header{Q", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_HeaderNameOf256Bytes_IsNotLookedUp()
    {
        Harness harness = new(writesLineFeedAsCrLf: true);
        string longest = new('h', 255);
        string tooLong = new('h', 256);
        harness.Variables.Headers[longest] = "L";
        harness.Variables.Headers[tooLong] = "T";

        await harness.RenderAsync("%header{" + longest + "}%header{" + tooLong + "}");

        Assert.AreEqual("L", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_NonAsciiText_IsWrittenAsUtf8()
    {
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("é%x😀");

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("é%x😀"), harness.StandardOutput.ToArray());
    }

    [TestMethod]
    public async Task RenderAsync_FailingWrite_StillClosesTheOpenedFile()
    {
        RecordingFileOpener files = new();
        WriteOutTemplateRenderer renderer = new(files, writesLineFeedAsCrLf: true, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using FailingStream standardError = new();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => renderer.RenderAsync("%output{f.txt}X%{nosuch}", new DictionaryVariableSource(), standardOutput, standardError));

        Assert.IsTrue(files.AllClosed);
        Assert.AreEqual("X", files.ContentOf("f.txt"));
    }

    [TestMethod]
    public async Task RenderAsync_StreamsThatCompleteLater_RenderTheSameBytes()
    {
        WriteOutTemplateRenderer renderer = new(new YieldingFileOpener(), writesLineFeedAsCrLf: true, TimeProvider.System);
        using YieldingStream standardOutput = new();
        using YieldingStream standardError = new();

        await renderer.RenderAsync("a%{stderr}b%{stdout}c%output{f.txt}d", new DictionaryVariableSource(), standardOutput, standardError);

        Assert.AreEqual("ac", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual("b", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RenderAsync_PreviousFileFailsToClose_StillClosesTheNewFile()
    {
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Files.FailingClose.Add("a.txt");

        await Assert.ThrowsExactlyAsync<IOException>(() => harness.RenderAsync("%output{a.txt}A%output{b.txt}B"));

        Assert.IsTrue(harness.Files.AllClosed);
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
        Assert.AreEqual(UnknownNoSuch, WriteOutTemplateRenderer.UnknownVariableWarning("nosuch"));
    }

    [TestMethod]
    public void UnknownVariableWarning_NullName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTemplateRenderer.UnknownVariableWarning(null!));
    }

    [TestMethod]
    public async Task RenderAsync_OnErrorAfterSuccess_StopsThere()
    {
        // curl -w "a\n%{onerror}b%{stderr}c" file:///c:/Windows/win.ini wrote "a\r\n" and nothing to standard error.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("a\\n%{onerror}b%{stderr}c");

        Assert.AreEqual("a\r\n", harness.StandardOutputText);
        Assert.AreEqual(string.Empty, harness.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_OnErrorAfterFailure_RendersNothingAndCarriesOn()
    {
        // curl -w "a%{onerror}b%{onerror}c" file:///c:/nonexist wrote "abc" and exited 37.
        Harness harness = new(writesLineFeedAsCrLf: true);
        harness.Variables.TransferFailed = true;

        await harness.RenderAsync("a%{onerror}b%{onerror}c");

        Assert.AreEqual("abc", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_Time_RendersTheFormattedTime()
    {
        // curl -w "[%time{%Y}]Q" wrote "[2026]Q"; %time{%Y]%{url} closes at the url's brace and prints nothing.
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("[%time{%Y-%m-%dT%H:%M:%S.%f%z}]Q|%time{%Y]%{url}|");

        Assert.AreEqual("[2026-09-27T03:30:08.545957+0000]Q||", harness.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnclosedTime_IsWrittenAsItStands()
    {
        // curl -w "[%time{%Y]" wrote "[%time{%Y]".
        Harness harness = new(writesLineFeedAsCrLf: true);

        await harness.RenderAsync("[%time{%Y]");

        Assert.AreEqual("[%time{%Y]", harness.StandardOutputText);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WriteOutTemplateRenderer(new RecordingFileOpener(), writesLineFeedAsCrLf: true, null!));
    }

    [TestMethod]
    public void Constructor_NullFileOpener_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WriteOutTemplateRenderer(null!, writesLineFeedAsCrLf: true, TimeProvider.System));
    }

    [TestMethod]
    public async Task RenderAsync_NullArgument_Throws()
    {
        WriteOutTemplateRenderer renderer = new(new RecordingFileOpener(), writesLineFeedAsCrLf: true, TimeProvider.System);
        DictionaryVariableSource variables = new();
        using MemoryStream stream = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync(null!, variables, stream, stream));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", null!, stream, stream));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", variables, null!, stream));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => renderer.RenderAsync("x", variables, stream, null!));
    }

    private sealed class Harness(bool writesLineFeedAsCrLf)
    {
        public DictionaryVariableSource Variables { get; } = new();

        public RecordingFileOpener Files { get; } = new();

        public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 27, 3, 30, 8, TimeSpan.Zero).AddTicks(5_459_570));

        public MemoryStream StandardOutput { get; } = new();

        public MemoryStream StandardError { get; } = new();

        public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput.ToArray());

        public string StandardErrorText => Encoding.UTF8.GetString(StandardError.ToArray());

        public Task RenderAsync(string template)
        {
            WriteOutTemplateRenderer renderer = new(Files, writesLineFeedAsCrLf, Clock);
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
