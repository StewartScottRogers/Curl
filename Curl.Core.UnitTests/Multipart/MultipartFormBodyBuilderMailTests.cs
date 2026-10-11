using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the mail messages <see cref="MultipartFormBodyBuilder.BuildMailMessageAsync" /> builds
/// against the ones curl 8.21.0 (<c>/mingw64/bin/curl</c>, Schannel) sent on 2026-10-10 for
/// <c>-F</c> parts to an <c>smtp://</c> URL, each recorded with
/// <c>Record-CurlExchange.ps1 -Smtp</c> and its DATA bytes copied here (BL-1988). Each test
/// injects the boundaries curl chose for that run.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderMailTests
{
    private const string HeaderFromFile = "X-fileheader1: This is a header from a file";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NestedAlternativeAndAttachmentMatchCurl()
    {
        // As upstream test646: -F "=(;type=multipart/alternative"
        // -F "= <body>This is the html version</body>;headers=X-test1: this is a header;type=text/html;headers=X-test2: this is another header "
        // -F "=This is the plain text version;headers=@hdr" -F "=)" -F "=@f.txt;headers=<hdr"
        // -H "From: different" -H "To: another" -H "Reply-To: <followup@example.com>"
        const string B = "------------------------3G6CNwfrjDzJAPNjdF7zVH";
        const string Inner = "------------------------bqS16GUtWfEGommVlQjULD";
        MultipartFormPart alternative = new(
            null,
            MultipartFormPartKind.Multipart,
            string.Empty,
            "multipart/alternative",
            null,
            NoHeaders,
            [
                new MultipartFormPart(null, MultipartFormPartKind.Text, "<body>This is the html version</body>", "text/html", null, ["X-test1: this is a header", "X-test2: this is another header"], NoParts),
                new MultipartFormPart(null, MultipartFormPartKind.Text, "This is the plain text version", null, null, [HeaderFromFile], NoParts),
            ]);
        MultipartFormPart attachment = new(null, MultipartFormPartKind.FileUpload, "f.txt", null, null, [HeaderFromFile], NoParts);

        MultipartFormBuildResult result = await BuildMailAsync(
            [B, Inner],
            ["From: different", "To: another", "Reply-To: <followup@example.com>"],
            alternative,
            attachment);

        string expected =
            $"Content-Type: multipart/mixed; boundary={B}\r\nMime-Version: 1.0\r\nFrom: different\r\nTo: another\r\nReply-To: <followup@example.com>\r\n\r\n"
            + $"--{B}\r\nContent-Type: multipart/alternative; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Type: text/html\r\nContent-Transfer-Encoding: 8bit\r\nX-test1: this is a header\r\nX-test2: this is another header\r\n\r\n<body>This is the html version</body>\r\n"
            + $"--{Inner}\r\n{HeaderFromFile}\r\n\r\nThis is the plain text version\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}\r\nContent-Disposition: attachment; filename=\"f.txt\"\r\n{HeaderFromFile}\r\n\r\nThis is an attached file.\n\nIt may contain any type of data.\n\r\n"
            + $"--{B}--\r\n";
        await AssertMessageAsync(result, expected, B);
    }

    [TestMethod]
    public async Task LabelsMatchCurlsMailStrategy()
    {
        // -F "=x;type=text/plain" -F "=@m.txt;type=text/plain" -F =@m.bin -F nm=val -F "=@m.txt;filename=a.html"
        const string B = "------------------------zCFu35LjhswOB0c57qdUCb";
        MultipartFormBuildResult result = await BuildMailAsync(
            [B],
            NoHeaders,
            new MultipartFormPart(null, MultipartFormPartKind.Text, "x", "text/plain", null, NoHeaders, NoParts),
            new MultipartFormPart(null, MultipartFormPartKind.FileUpload, "m.txt", "text/plain", null, NoHeaders, NoParts),
            new MultipartFormPart(null, MultipartFormPartKind.FileUpload, "m.bin", null, null, NoHeaders, NoParts),
            new MultipartFormPart("nm", MultipartFormPartKind.Text, "val", null, null, NoHeaders, NoParts),
            new MultipartFormPart(null, MultipartFormPartKind.FileUpload, "m.txt", null, "a.html", NoHeaders, NoParts));

        string expected =
            $"Content-Type: multipart/mixed; boundary={B}\r\nMime-Version: 1.0\r\n\r\n"
            + $"--{B}\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: 8bit\r\n\r\nx\r\n"
            + $"--{B}\r\nContent-Disposition: attachment; filename=\"m.txt\"\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: 8bit\r\n\r\nSome text\r\n\r\n"
            + $"--{B}\r\nContent-Disposition: attachment; filename=\"m.bin\"\r\nContent-Type: application/octet-stream\r\nContent-Transfer-Encoding: 8bit\r\n\r\nSome text\r\n\r\n"
            + $"--{B}\r\nContent-Disposition: attachment; name=\"nm\"\r\n\r\nval\r\n"
            + $"--{B}\r\nContent-Disposition: attachment; filename=\"a.html\"\r\nContent-Type: text/html\r\nContent-Transfer-Encoding: 8bit\r\n\r\nSome text\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertMessageAsync(result, expected, B);
    }

    [TestMethod]
    public async Task SevenBitFileAboveOneHundredTwentySevenFailsOnlyTheReadThatReachesIt()
    {
        // -F "=@b.txt;encoder=7bit" with b.txt holding A, 0xE9, B, LF: curl sends EHLO, MAIL, RCPT
        // and DATA, then fails with exit 26, "read error getting mime data", as upstream test649 expects.
        const string B = "------------------------mFA0IV5QMe0CKeqYrIDbst";
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormBuildResult result = await BuildMailAsync(
            [B],
            NoHeaders,
            new MultipartFormPart(null, MultipartFormPartKind.FileUpload, "b.txt", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        await AssertSentBeforeRefusalAsync(
            result,
            $"Content-Type: multipart/mixed; boundary={B}\r\nMime-Version: 1.0\r\n\r\n--{B}\r\n"
            + "Content-Disposition: attachment; filename=\"b.txt\"\r\nContent-Transfer-Encoding: 7bit\r\n\r\nA");
    }

    [TestMethod]
    public async Task SevenBitTextAboveOneHundredTwentySevenSendsTheBytesBeforeItThenFails()
    {
        // -F "=AéB;encoder=7bit": curl 8.21.0 builds the message, sends MAIL with its SIZE= and
        // DATA, then the part headers and the A, then fails with exit 26 (measured, BL-2025).
        const string B = "------------------------hXp3I0f1iBBtDtGkyMatVe";
        MultipartFormBuildResult result = await BuildMailAsync(
            [B],
            NoHeaders,
            new MultipartFormPart(null, MultipartFormPartKind.Text, "AéB", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        Assert.IsTrue(result.IsBuilt);
        TestDiagnostics.For(TestContext).Assert("length is known", true, result.Body.Length is not null);
        Assert.IsNotNull(result.Body.Length);
        await AssertSentBeforeRefusalAsync(
            result,
            $"Content-Type: multipart/mixed; boundary={B}\r\nMime-Version: 1.0\r\n\r\n--{B}\r\n"
            + "Content-Transfer-Encoding: 7bit\r\n\r\nA");
    }

    [TestMethod]
    public async Task QuotedPrintablePartLeavesTheMessageSizeUnknownAndUnseekable()
    {
        // -F "=@m.txt;encoder=quoted-printable": curl 8.21.0 sends MAIL FROM with no SIZE= and
        // refuses to APPEND it to IMAP, exit 25, as the message's size is unknown (measured, BL-2025).
        const string B = "------------------------SYrs9NokL0hBbO6azfkoIl";
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormBuildResult result = await BuildMailAsync(
            [B],
            NoHeaders,
            new MultipartFormPart(null, MultipartFormPartKind.FileUpload, "m.txt", null, null, NoHeaders, NoParts) { Encoder = "quoted-printable" },
            new MultipartFormPart(null, MultipartFormPartKind.Text, "hi", null, null, NoHeaders, NoParts) { Encoder = "base64" });

        Assert.IsTrue(result.IsBuilt);
        await using Stream content = result.Body.Content;
        diagnostics.Assert("length", null, result.Body.Length);
        Assert.IsNull(result.Body.Length);
        diagnostics.Assert("can seek", false, content.CanSeek);
        Assert.IsFalse(content.CanSeek);
        using MemoryStream copy = new();
        await content.CopyToAsync(copy, TestContext.CancellationToken);
        string expected = $"Content-Type: multipart/mixed; boundary={B}\r\nMime-Version: 1.0\r\n\r\n--{B}\r\n"
            + "Content-Disposition: attachment; filename=\"m.txt\"\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n"
            + $"Some text\r\n\r\n--{B}\r\nContent-Transfer-Encoding: base64\r\n\r\naGk=\r\n--{B}--\r\n";
        string actual = Encoding.Latin1.GetString(copy.ToArray());
        diagnostics.Diff("message", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private async Task AssertSentBeforeRefusalAsync(MultipartFormBuildResult result, string expectedBeforeRefusal)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        await using Stream content = result.Body.Content;
        using MemoryStream sent = new();
        byte[] buffer = new byte[4096];
        RequestBodyReadFailedException failure = await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(async () =>
        {
            int read;
            while ((read = await content.ReadAsync(buffer, TestContext.CancellationToken)) > 0)
            {
                sent.Write(buffer, 0, read);
            }
        });
        string actual = Encoding.Latin1.GetString(sent.ToArray());
        diagnostics.Diff("sent before the refusal", expectedBeforeRefusal, actual);
        Assert.AreEqual(expectedBeforeRefusal, actual);
        diagnostics.Assert("message", MultipartFormBodyBuilder.ReadFailedMessage, failure.Message);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, failure.Message);
    }

    [TestMethod]
    public async Task NullPartsOrHeadersThrow()
    {
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, MultipartBoundary.CreateRandom);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => builder.BuildMailMessageAsync(null!, MultipartNameEscaping.Percent, NoHeaders, TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => builder.BuildMailMessageAsync(NoParts, MultipartNameEscaping.Percent, null!, TestContext.CancellationToken).AsTask());
    }

    private async Task<MultipartFormBuildResult> BuildMailAsync(string[] boundaries, string[] messageHeaders, params MultipartFormPart[] parts)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("boundaries", string.Join(",", boundaries));
        diagnostics.Arrange("part count", parts.Length);
        FormFileSystem files = new FormFileSystem()
            .WithFile("f.txt", "This is an attached file.\n\nIt may contain any type of data.\n")
            .WithFile("m.txt", "Some text\r\n")
            .WithFile("m.bin", "Some text\r\n")
            .WithFile("b.txt", [0x41, 0xE9, 0x42, 0x0A]);
        Queue<string> queue = new(boundaries);
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, () => queue.Count > 1 ? queue.Dequeue() : queue.Peek());
        MultipartFormBuildResult result;
        using (diagnostics.Phase("build"))
        {
            result = await builder.BuildMailMessageAsync(parts, MultipartNameEscaping.Percent, messageHeaders, TestContext.CancellationToken);
        }

        diagnostics.Act("is built", result.IsBuilt);
        return result;
    }

    private async Task AssertMessageAsync(MultipartFormBuildResult result, string expected, string boundary)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("content type", $"multipart/mixed; boundary={boundary}", result.Body.ContentType);
        Assert.AreEqual($"multipart/mixed; boundary={boundary}", result.Body.ContentType);
        diagnostics.Assert("length", (long)expected.Length, result.Body.Length);
        Assert.AreEqual(expected.Length, result.Body.Length);
        using MemoryStream copy = new();
        await using (result.Body.Content)
        {
            await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
        }

        string actual = Encoding.Latin1.GetString(copy.ToArray());
        diagnostics.Diff("message", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
