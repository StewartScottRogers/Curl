using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Adversarial black-box tests for <see cref="WriteOutTemplateRenderer"/> (BL-1504): its
/// buffer limits on both sides, malformed templates, and repeated and concurrent renders.
/// Where curl's output is pinned, it was measured on 2026-10-07 with curl 8.21.0 (mingw,
/// Schannel), <c>curl -s -o NUL -w &lt;template&gt; file:///nonexist</c>.
/// </summary>
[TestClass]
public sealed class WriteOutTemplateRendererAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RenderAsync_EmptyVariableName_WarnsWithEmptyQuotesAndWritesNothing()
    {
        // curl -w "a%{}b" wrote "ab" and "curl: unknown --write-out variable: ''".
        Fixture fixture = new();

        await fixture.RenderAsync("a%{}b", TestContext);

        Assert.AreEqual("ab", fixture.StandardOutputText);
        Assert.AreEqual("curl: unknown --write-out variable: ''\n", fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNameOf23Bytes_IsLookedUpAndWarnedAbout()
    {
        // curl -w "1%{<23 a>}2|" wrote "12|" and the warning naming all 23 bytes.
        string name = new('a', 23);
        Fixture fixture = new();

        await fixture.RenderAsync("1%{" + name + "}2|", TestContext);

        Assert.AreEqual("12|", fixture.StandardOutputText);
        Assert.AreEqual($"curl: unknown --write-out variable: '{name}'\n", fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNameOf24Bytes_EndsTheRenderingSilently()
    {
        // curl -w "1%{<24 a>}2|" wrote "1" and nothing on standard error.
        Fixture fixture = new();

        await fixture.RenderAsync("1%{" + new string('a', 24) + "}2|", TestContext);

        Assert.AreEqual("1", fixture.StandardOutputText);
        Assert.AreEqual(string.Empty, fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_VariableNameOf12TwoByteCharacters_CountsBytesNotCharactersAndEnds()
    {
        Fixture fixture = new();
        fixture.Variables.Values[new string('é', 12)] = "value";

        await fixture.RenderAsync("1%{" + new string('é', 12) + "}2", TestContext);

        Assert.AreEqual("1", fixture.StandardOutputText);
        Assert.AreEqual(string.Empty, fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_HeaderNameOf255Bytes_RendersTheHeader()
    {
        string name = new('h', 255);
        Fixture fixture = new();
        fixture.Variables.Headers[name] = "found";

        await fixture.RenderAsync("<%header{" + name + "}>", TestContext);

        Assert.AreEqual("<found>", fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_HeaderNameOf256Bytes_RendersNothingAndCarriesOn()
    {
        string name = new('h', 256);
        Fixture fixture = new();
        fixture.Variables.Headers[name] = "found";

        await fixture.RenderAsync("<%header{" + name + "}>", TestContext);

        Assert.AreEqual("<>", fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_OutputFileNameOf511Bytes_IsOpened()
    {
        string path = new('f', 511);
        Fixture fixture = new();

        await fixture.RenderAsync("a%output{" + path + "}b", TestContext);

        CollectionAssert.AreEqual(new[] { path }, fixture.Files.OpenedPaths);
        Assert.AreEqual("a", fixture.StandardOutputText);
        Assert.AreEqual("b", fixture.Files.TextOf(path));
    }

    [TestMethod]
    public async Task RenderAsync_OutputFileNameOf512Bytes_IsNotOpenedAndOutputStaysPut()
    {
        Fixture fixture = new();

        await fixture.RenderAsync("a%output{" + new string('f', 512) + "}b", TestContext);

        Assert.AreEqual(0, fixture.Files.OpenedPaths.Count);
        Assert.AreEqual("ab", fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_UnterminatedHeaderOutputAndTime_AreWrittenAsTheyStand()
    {
        // curl -w "x%header{abc|%output{def|%time{ghi" wrote the template unchanged.
        Fixture fixture = new();

        await fixture.RenderAsync("x%header{abc|%output{def|%time{ghi", TestContext);

        Assert.AreEqual("x%header{abc|%output{def|%time{ghi", fixture.StandardOutputText);
        Assert.AreEqual(0, fixture.Files.OpenedPaths.Count);
    }

    [TestMethod]
    public async Task RenderAsync_PercentAsVariableName_WarnsAboutThePercent()
    {
        // curl -w "%{%}" wrote nothing and "curl: unknown --write-out variable: '%'".
        Fixture fixture = new();

        await fixture.RenderAsync("%{%}", TestContext);

        Assert.AreEqual(string.Empty, fixture.StandardOutputText);
        Assert.AreEqual("curl: unknown --write-out variable: '%'\n", fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_EmptyOutputFileNameWithAppend_AsksTheOpenerAndStaysOnStandardOutputWhenRefused()
    {
        Fixture fixture = new();
        fixture.Files.Unopenable.Add(string.Empty);

        await fixture.RenderAsync("a%output{>>}b", TestContext);

        CollectionAssert.AreEqual(new[] { ">>" + string.Empty }, fixture.Files.Attempts);
        Assert.AreEqual("ab", fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_ValueWithUnpairedSurrogate_WritesTheReplacementCharacterWithoutThrowing()
    {
        Fixture fixture = new();
        fixture.Variables.Values["v"] = "a\uD800b\uDC00c";

        await fixture.RenderAsync("%{v}", TestContext);

        CollectionAssert.AreEqual("a�b�c"u8.ToArray(), fixture.StandardOutput.ToArray());
    }

    [TestMethod]
    public async Task RenderAsync_ControlCharactersAndNulInTemplate_PassThroughUnchanged()
    {
        Fixture fixture = new();
        string template = "\0\u0001\u001b[31m\u007f|";

        await fixture.RenderAsync(template, TestContext);

        Assert.AreEqual(template, fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_BackslashBeforeVariable_KeepsTheBackslashAndRendersTheVariable()
    {
        Fixture fixture = new();
        fixture.Variables.Values["v"] = "1";

        await fixture.RenderAsync("\\%{v}", TestContext);

        Assert.AreEqual("\\%{v}", fixture.StandardOutputText);
    }

    [TestMethod]
    public async Task RenderAsync_TenThousandVariables_RendersEveryOne()
    {
        Fixture fixture = new();
        fixture.Variables.Values["v"] = "ab";

        await fixture.RenderAsync(string.Concat(Enumerable.Repeat("%{v}", 10_000)), TestContext);

        Assert.AreEqual(20_000, fixture.StandardOutputText.Length);
        Assert.AreEqual(string.Empty, fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_StdoutStderrSwitchedManyTimes_KeepsEachPieceOnItsStream()
    {
        Fixture fixture = new();

        await fixture.RenderAsync(string.Concat(Enumerable.Repeat("o%{stderr}e%{stdout}", 500)), TestContext);

        Assert.AreEqual(new string('o', 500), fixture.StandardOutputText);
        Assert.AreEqual(new string('e', 500), fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_OnerrorOnSuccessfulTransfer_StopsBeforeFileIsOpenedAndClosesNothingOpen()
    {
        Fixture fixture = new();

        await fixture.RenderAsync("a%output{one}b%{onerror}c%output{two}d", TestContext);

        CollectionAssert.AreEqual(new[] { "one" }, fixture.Files.OpenedPaths);
        Assert.AreEqual("b", fixture.Files.TextOf("one"));
        Assert.IsTrue(fixture.Files.AllDisposed);
    }

    [TestMethod]
    public async Task RenderAsync_SameRendererTwice_WritesTheSameBytesEachTime()
    {
        Fixture fixture = new();
        fixture.Variables.Values["v"] = "x";
        WriteOutTemplateRenderer renderer = fixture.CreateRenderer();

        await renderer.RenderAsync("<%{v}%{nosuch}>", fixture.Variables, fixture.StandardOutput, fixture.StandardError, TestContext.CancellationToken);
        await renderer.RenderAsync("<%{v}%{nosuch}>", fixture.Variables, fixture.StandardOutput, fixture.StandardError, TestContext.CancellationToken);

        Assert.AreEqual("<x><x>", fixture.StandardOutputText);
        Assert.AreEqual("curl: unknown --write-out variable: 'nosuch'\ncurl: unknown --write-out variable: 'nosuch'\n", fixture.StandardErrorText);
    }

    [TestMethod]
    public async Task RenderAsync_OneRendererOnManyTasksAtOnce_GivesEachTheSameOutput()
    {
        Fixture fixture = new();
        fixture.Variables.Values["v"] = "x";
        WriteOutTemplateRenderer renderer = fixture.CreateRenderer();
        MemoryStream[] outputs = [.. Enumerable.Range(0, 32).Select(_ => new MemoryStream())];

        await Task.WhenAll(outputs.Select(output => Task.Run(
            () => renderer.RenderAsync("a%{v}b\\n%{v}%%", fixture.Variables, output, Stream.Null, TestContext.CancellationToken),
            TestContext.CancellationToken)));

        foreach (MemoryStream output in outputs)
        {
            Assert.AreEqual("axb\nx%", Encoding.UTF8.GetString(output.ToArray()));
        }
    }

    [TestMethod]
    public async Task RenderAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceled()
    {
        Fixture fixture = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.CreateRenderer().RenderAsync(
            "text", fixture.Variables, fixture.StandardOutput, fixture.StandardError, cancellation.Token));

        Assert.AreEqual(0, fixture.StandardOutput.Length);
    }

    [TestMethod]
    public void UnknownVariableWarning_NameWithQuoteAndLineFeed_IsEmbeddedVerbatim()
    {
        Assert.AreEqual("curl: unknown --write-out variable: 'a'\nb'", WriteOutTemplateRenderer.UnknownVariableWarning("a'\nb"));
    }

    private sealed class Fixture
    {
        public VariableSource Variables { get; } = new();

        public FileOpener Files { get; } = new();

        public MemoryStream StandardOutput { get; } = new();

        public MemoryStream StandardError { get; } = new();

        public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput.ToArray());

        public string StandardErrorText => Encoding.UTF8.GetString(StandardError.ToArray());

        public WriteOutTemplateRenderer CreateRenderer()
        {
            return new WriteOutTemplateRenderer(
                Files,
                writesLineFeedAsCrLf: false,
                WriteOutTimeDialect.Glibc,
                new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        }

        public async Task RenderAsync(string template, TestContext testContext)
        {
            TestDiagnostics diagnostics = TestDiagnostics.For(testContext);
            diagnostics.Arrange("template", template.Length > 200 ? template[..200] + "…" : template);
            await CreateRenderer().RenderAsync(template, Variables, StandardOutput, StandardError, testContext.CancellationToken);
            diagnostics.Act("standard output", StandardOutputText);
            diagnostics.Act("standard error", StandardErrorText);
        }
    }

    private sealed class VariableSource : IWriteOutVariableSource
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            return Values.TryGetValue(name, out text);
        }

        public string? FindFirstHeaderValue(string name)
        {
            return Headers.GetValueOrDefault(name);
        }
    }

    private sealed class FileOpener : IWriteOutFileOpener
    {
        private readonly Dictionary<string, TrackedStream> files = new(StringComparer.Ordinal);

        public HashSet<string> Unopenable { get; } = new(StringComparer.Ordinal);

        public List<string> Attempts { get; } = [];

        public List<string> OpenedPaths { get; } = [];

        public bool AllDisposed => files.Values.All(file => file.IsDisposed);

        public string TextOf(string path)
        {
            return Encoding.UTF8.GetString(files[path].Contents);
        }

        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            Attempts.Add((append ? ">>" : string.Empty) + path);
            if (Unopenable.Contains(path))
            {
                stream = null;
                return false;
            }

            TrackedStream file = new();
            files[path] = file;
            OpenedPaths.Add(path);
            stream = file;
            return true;
        }
    }

    private sealed class TrackedStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        public byte[] Contents { get; private set; } = [];

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                Contents = ToArray();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
