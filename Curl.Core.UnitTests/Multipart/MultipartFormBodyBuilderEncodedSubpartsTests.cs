using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins a part with subparts and an encoder, as libcurl 8.22.0 sends it for
/// <c>curl_mime_subparts</c> plus <c>curl_mime_encoder(part, "base64")</c> (BL-3210 case 3,
/// <c>Documentation/Product/LibcurlMimeEdgeCaseRecordings.md</c>): the nested block, boundaries and
/// headers included, is encoded and sent under <c>Content-Transfer-Encoding</c>.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderEncodedSubpartsTests
{
    private const string Outer = "------------------------0000000000000000000000";
    private const string Inner = "------------------------1111111111111111111111";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Base64EncoderEncodesTheWholeNestedBlockMatchingLibcurl()
    {
        string block = $"--{Inner}\r\nContent-Disposition: attachment; name=\"b\"\r\n\r\nhello\r\n--{Inner}--\r\n";
        string encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(block));
        string expectedEncoded = string.Join("\r\n", Enumerable.Range(0, (encoded.Length + 75) / 76).Select(i => encoded.Substring(i * 76, Math.Min(76, encoded.Length - (i * 76)))));

        string text = await BuildTextAsync("base64");

        Assert.AreEqual(
            $"--{Outer}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n"
            + $"Content-Transfer-Encoding: base64\r\n\r\n{expectedEncoded}\r\n--{Outer}--\r\n",
            text);
        Assert.AreEqual(3, expectedEncoded.Split("\r\n").Length);
    }

    [TestMethod]
    public async Task EightBitEncoderLabelsTheNestedBlockAndSendsItAsItIs()
    {
        string text = await BuildTextAsync("8bit");

        Assert.AreEqual(
            $"--{Outer}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n"
            + $"Content-Transfer-Encoding: 8bit\r\n\r\n--{Inner}\r\nContent-Disposition: attachment; name=\"b\"\r\n\r\nhello\r\n--{Inner}--\r\n\r\n--{Outer}--\r\n",
            text);
    }

    [TestMethod]
    public async Task SevenBitEncoderOverNonAsciiSubpartFailsTheReadThatReachesIt()
    {
        MultipartFormBuildResult result = await BuildAsync("7bit", "héllo", null);

        Assert.IsTrue(result.IsBuilt);
        using MemoryStream copy = new();
        await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(async () => await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken));
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    public async Task ASubpartWithRefusedDataFailsTheBodyBeforeSending()
    {
        MultipartFormBuildResult result = await BuildAsync("base64", "héllo", "7bit");

        Assert.IsFalse(result.IsBuilt);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure.ExitCode);
    }

    [TestMethod]
    public async Task AFailingSubpartFailsTheBody()
    {
        MultipartFormBuildResult result = await BuildAsync("base64", "hello", "bogus");

        Assert.IsFalse(result.IsBuilt);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.Failure.ExitCode);
    }

    private async Task<string> BuildTextAsync(string encoder)
    {
        MultipartFormBuildResult result = await BuildAsync(encoder, "hello", null);
        Assert.IsTrue(result.IsBuilt);
        using MemoryStream copy = new();
        await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
        await result.Body.Content.DisposeAsync();
        Assert.AreEqual(copy.Length, result.Body.Length);
        return Encoding.Latin1.GetString(copy.ToArray());
    }

    private async Task<MultipartFormBuildResult> BuildAsync(string encoder, string value, string? subpartEncoder)
    {
        Queue<string> boundaries = new([Outer, Inner]);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, boundaries.Dequeue);
        MultipartFormPart sub = new("b", MultipartFormPartKind.Text, value, null, null, NoHeaders, NoParts) { Encoder = subpartEncoder };
        MultipartFormPart part = new("a", MultipartFormPartKind.Multipart, string.Empty, null, null, NoHeaders, [sub]) { Encoder = encoder };
        return await builder.BuildAsync([part], TestContext.CancellationToken);
    }
}
