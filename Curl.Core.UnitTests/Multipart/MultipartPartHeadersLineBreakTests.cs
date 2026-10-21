using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins every row of upstream curl 8.22.0's <c>tests/unit/unit3227.c</c>: a CR or LF in a mail
/// part's name or file name is refused with <c>CURLE_BAD_FUNCTION_ARGUMENT</c> (exit 43), as
/// <c>Curl_mime_prepare_headers</c> with <c>MIMESTRATEGY_MAIL</c> refuses it, while the form
/// strategy writes it as <c>%0D</c> and <c>%0A</c> and splits no header line (BL-2907).
/// </summary>
[TestClass]
public sealed class MultipartPartHeadersLineBreakTests
{
    private const string Boundary = "------------------------0000000000000000000000";

    private const string InjectedFileName = "a\r\nX-Injected: 1";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object?[]> RefusedMailParts =>
    [
        ["CRLF filename", null, InjectedFileName],
        ["LF name", "a\nb", null],
        ["CR filename", null, "a\rb"],
    ];

    [TestMethod]
    [DynamicData(nameof(RefusedMailParts))]
    public async Task MailPartWithLineBreakIsRefusedWithExit43(string row, string? name, string? fileName)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("row", row);
        MultipartFormPart part = TextPart(name, fileName);

        bool refused = MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: true);
        MultipartFormBuildResult result = await BuildMailAsync(part);

        diagnostics.Assert("refused", true, refused);
        Assert.IsTrue(refused, row);
        diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, result.Failure?.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.Failure?.ExitCode, row);
        diagnostics.Assert("message", MultipartFormBodyBuilder.LineBreakInNameMessage, result.Failure?.ErrorMessage);
        Assert.AreEqual("A libcurl function was given a bad argument", result.Failure?.ErrorMessage, row);
    }

    [TestMethod]
    public async Task PlainMailPartIsAcceptedWithCleanHeaders()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormPart part = TextPart("field", "readme.txt");

        bool refused = MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: true);
        string block = MultipartPartHeaders.Format(part, null, null, null, MultipartNameEscaping.Percent, forMail: true, out _);
        MultipartFormBuildResult result = await BuildMailAsync(part);

        diagnostics.Assert("refused", false, refused);
        Assert.IsFalse(refused);
        diagnostics.Assert("block", "Content-Disposition: attachment; name=\"field\"; filename=\"readme.txt\"\r\n\r\n", block);
        Assert.AreEqual("Content-Disposition: attachment; name=\"field\"; filename=\"readme.txt\"\r\n\r\n", block);
        Assert.IsFalse(SplitsALine(block), "clean mail header split");
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    public async Task FormPartWithLineBreakIsAcceptedAndPercentEncoded()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormPart part = TextPart(null, InjectedFileName);

        bool refused = MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: false);
        string block = MultipartPartHeaders.Format(part, "form-data", null, null, MultipartNameEscaping.Percent, forMail: false, out _);
        MultipartFormBuildResult result = await Builder().BuildAsync([part], TestContext.CancellationToken);

        diagnostics.Assert("refused", false, refused);
        Assert.IsFalse(refused);
        diagnostics.Assert("block", "Content-Disposition: form-data; filename=\"a%0D%0AX-Injected: 1\"\r\n\r\n", block);
        Assert.AreEqual("Content-Disposition: form-data; filename=\"a%0D%0AX-Injected: 1\"\r\n\r\n", block);
        Assert.IsFalse(SplitsALine(block), "form header split despite encoding");
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    public async Task FormEscapePartWithLineBreakIsRefusedWithExit43()
    {
        // mime.c refuses under data->set.mime_formescape too: backslash quoting cannot write a line break.
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormPart part = TextPart("a\r\nb", null);

        MultipartFormBuildResult result = await Builder().BuildAsync([part], MultipartNameEscaping.Backslash, TestContext.CancellationToken);

        diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, result.Failure?.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.Failure?.ExitCode);
    }

    [TestMethod]
    public void MailPartWithItsOwnContentDispositionIsNotRefused()
    {
        // curl writes no Content-Disposition for a part that has one, so nothing can split.
        MultipartFormPart part = TextPart("a\nb", null) with { Headers = ["Content-Disposition: inline"] };

        bool refused = MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: true);

        TestDiagnostics.For(TestContext).Assert("refused", false, refused);
        Assert.IsFalse(refused);
    }

    [TestMethod]
    public void MailFilePartWhoseBaseNameHoldsALineBreakIsRefused()
    {
        // curl_mime_filedata sets the file name to the path's base name.
        MultipartFormPart part = new(null, MultipartFormPartKind.FileUpload, "dir/a\nb.txt", null, null, NoHeaders, NoParts);

        bool refused = MultipartPartHeaders.RefusesLineBreakInName(part, MultipartNameEscaping.Percent, forMail: true);

        TestDiagnostics.For(TestContext).Assert("refused", true, refused);
        Assert.IsTrue(refused);
    }

    /// <summary>As unit3227's <c>headers_have_ctrl</c>: whether any header line holds a raw CR or LF.</summary>
    private static bool SplitsALine(string block) =>
        block.Split("\r\n").Any(line => line.AsSpan().IndexOfAny('\r', '\n') >= 0);

    private static MultipartFormPart TextPart(string? name, string? fileName) =>
        new(name, MultipartFormPartKind.Text, "x", null, fileName, NoHeaders, NoParts);

    private static MultipartFormBodyBuilder Builder() => new(new FormFileSystem(), Encoding.UTF8, () => Boundary);

    private async Task<MultipartFormBuildResult> BuildMailAsync(MultipartFormPart part) =>
        await Builder().BuildMailMessageAsync([part], MultipartNameEscaping.Percent, NoHeaders, TestContext.CancellationToken);
}
