using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins when a <c>7bit</c> part over a byte above 127 fails. libcurl 8.22.0 starts the request: the
/// headers, the part's headers and the bytes before the refused one go out, then the read fails
/// and <c>curl_easy_perform</c> gives 26 (measured, BL-3210 case 2: 296 bytes on the wire, the
/// last body byte <c>h</c>). The curl tool fails before connecting, also with exit 26 (measured
/// 2026-10-22, <c>-F "a=&lt;file;encoder=7bit"</c>), which is the builder's default.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderSevenBitTests
{
    private const string Boundary = "------------------------sevenbitboundary01";

    private const string SentBeforeRefusal =
        $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Transfer-Encoding: 7bit\r\n\r\nh";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TextPartOverNonAsciiFailsWhileSendingWhenTheBuilderWaitsForTheSend()
    {
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "h\u00e9llo", null, null, NoHeaders, NoParts) { Encoder = "7bit" };

        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), waitsForSend: true, part);

        await AssertFailsAfterAsync(result, SentBeforeRefusal);
    }

    [TestMethod]
    public async Task SeekableFileOverNonAsciiFailsWhileSendingWhenTheBuilderWaitsForTheSend()
    {
        FormFileSystem files = new FormFileSystem().WithFile("h.txt", [0x68, 0xC3, 0xA9]);
        MultipartFormPart part = new("a", MultipartFormPartKind.FileContent, "h.txt", null, null, NoHeaders, NoParts) { Encoder = "7bit" };

        MultipartFormBuildResult result = await BuildAsync(files, waitsForSend: true, part);

        await AssertFailsAfterAsync(result, SentBeforeRefusal);
    }

    [TestMethod]
    public async Task TextPartOverNonAsciiFailsBeforeConnectingByDefault()
    {
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "h\u00e9llo", null, null, NoHeaders, NoParts) { Encoder = "7bit" };

        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), waitsForSend: false, part);

        Assert.IsFalse(result.IsBuilt);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
    }

    [TestMethod]
    public async Task AsciiTextPartIsBuiltWhenTheBuilderWaitsForTheSend()
    {
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "hello", null, null, NoHeaders, NoParts) { Encoder = "7bit" };

        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), waitsForSend: true, part);

        Assert.IsTrue(result.IsBuilt);
        await using Stream content = result.Body.Content;
        using StreamReader reader = new(content, Encoding.Latin1);
        string sent = await reader.ReadToEndAsync(TestContext.CancellationToken);
        Assert.AreEqual($"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Transfer-Encoding: 7bit\r\n\r\nhello\r\n--{Boundary}--\r\n", sent);
    }

    private async Task AssertFailsAfterAsync(MultipartFormBuildResult result, string expectedBeforeRefusal)
    {
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
        Assert.AreEqual(expectedBeforeRefusal, Encoding.Latin1.GetString(sent.ToArray()));
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, failure.Message);
    }

    private ValueTask<MultipartFormBuildResult> BuildAsync(FormFileSystem files, bool waitsForSend, MultipartFormPart part)
    {
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, () => Boundary) { SevenBitRefusalWaitsForSend = waitsForSend };
        return builder.BuildAsync([part], TestContext.CancellationToken);
    }
}
