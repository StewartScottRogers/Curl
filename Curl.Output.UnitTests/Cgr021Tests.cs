using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins CGR-021: <c>%{content_type}</c>, <c>%header{…}</c> and <c>%{header_json}</c> write
/// the response's header bytes unchanged, as curl 8.22.0 (mingw, Schannel) did on 2026-10-26,
/// not re-encoded from Latin-1 to UTF-8. The commands are in BL-3550's Notes.
/// </summary>
[TestClass]
public sealed class Cgr021Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RenderAsync_HighBitHeaderBytes_WritesThemRawAsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nX-Hi: caf\xc3\xa9 \xff\xfe end\r\n
        // Content-Type: text/\xe9\r\nX-\xc3\xa9: v\r\nContent-Length: 2\r\n\r\nok'
        // -CurlArgs -s,-o,NUL,-w,'%{content_type}|%header{x-hi}|%{header_json}\n'
        TransferReport report = new()
        {
            ContentType = Latin1("text/\xe9"),
            ResponseHeaders =
            [
                new("X-Hi", Latin1("caf\xc3\xa9 \xff\xfe end")),
                new("Content-Type", Latin1("text/\xe9")),
                new(Latin1("X-\xc3\xa9"), "v"),
                new("Content-Length", "2"),
            ],
        };
        diagnostics.Arrange("header count", report.ResponseHeaders.Count);

        byte[] written = await RenderAsync("%{content_type}|%header{x-hi}|%{header_json}\\n", report);
        diagnostics.Act("written", Convert.ToHexString(written));

        byte[] expected = Encoding.Latin1.GetBytes(
            "text/\xe9|caf\xc3\xa9 \xff\xfe end|{\"x-hi\":[\"caf\xc3\xa9 \xff\xfe end\"],\r\n" +
            "\"content-type\":[\"text/\xe9\"],\r\n\"x-\xc3\xa9\":[\"v\"],\r\n\"content-length\":[\"2\"]\r\n}\r\n");
        diagnostics.Diff("written", Convert.ToHexString(expected), Convert.ToHexString(written));
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task RenderAsync_Latin1AndUtf8HeaderValue_WritesBothRawAsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Response 'HTTP/1.1 200 OK\r\nX-A: caf\xe9 \xc3\xa9\r\nContent-Type: t/\xe9\r\nContent-Length: 2\r\n\r\nok'
        // with -s,-S,-o,NUL,-w,'[%header{x-a}][%{content_type}]' printed [caf\xe9 \xc3\xa9][t/\xe9].
        TransferReport report = new()
        {
            ContentType = Latin1("t/\xe9"),
            ResponseHeaders = [new("X-A", Latin1("caf\xe9 \xc3\xa9")), new("Content-Type", Latin1("t/\xe9"))],
        };
        diagnostics.Arrange("header count", report.ResponseHeaders.Count);

        byte[] written = await RenderAsync("[%header{x-a}][%{content_type}]", report);
        diagnostics.Act("written", Convert.ToHexString(written));

        byte[] expected = Encoding.Latin1.GetBytes("[caf\xe9 \xc3\xa9][t/\xe9]");
        diagnostics.Diff("written", Convert.ToHexString(expected), Convert.ToHexString(written));
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task RenderAsync_TemplateTextBesideRawHeaderBytes_StaysUtf8()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new() { ResponseHeaders = [new("X-A", Latin1("\xe9"))] };
        diagnostics.Arrange("template", "é then 😀 then the header");

        byte[] written = await RenderAsync("é😀%header{x-a:all:,}", report);
        diagnostics.Act("written", Convert.ToHexString(written));

        byte[] expected = [.. Encoding.UTF8.GetBytes("é😀"), 0xE9];
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    public void MarkHeaderBytes_KeepsCharactersOutsideTheHighByteRange()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", "a, U+0080, U+00FF, U+0100");

        string marked = WriteOutTemplateRenderer.MarkHeaderBytes("a\u0080ÿĀ");
        diagnostics.Act("marked", marked);

        Assert.AreEqual("aĀ", marked);
    }

    [TestMethod]
    public void MarkHeaderBytes_Null_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", "null");

        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTemplateRenderer.MarkHeaderBytes(null!));
    }

    [TestMethod]
    public void FindFirstHeaderValue_NonAsciiName_MatchesTheHeaderNameBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new() { ResponseHeaders = [new(Latin1("X-\xc3\xa9"), "v")] };
        diagnostics.Arrange("header name bytes", "X-\\xc3\\xa9");

        string? value = WithReport(report).FindFirstHeaderValue("x-é");
        diagnostics.Act("value", value);

        Assert.AreEqual("v", value);
    }

    /// <summary>A header text as the response head holds it: one byte per character.</summary>
    private static string Latin1(string bytes) => bytes;

    private static TransferWriteOutVariables WithReport(TransferReport report)
    {
        return new TransferWriteOutVariables(TransferResult.Success(0) with { Report = report }, "http://h/", 0, "http://h/", "http", TimeProvider.System);
    }

    private static async Task<byte[]> RenderAsync(string template, TransferReport report)
    {
        WriteOutTemplateRenderer renderer = new(new NoFileOpener(), writesLineFeedAsCrLf: true, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        await renderer.RenderAsync(template, WithReport(report), standardOutput, standardError);
        return standardOutput.ToArray();
    }

    private sealed class NoFileOpener : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }
    }
}
