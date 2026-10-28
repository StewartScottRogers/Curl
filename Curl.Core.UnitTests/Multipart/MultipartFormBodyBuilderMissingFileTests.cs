using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins a file part whose file is missing: the body libcurl 8.22.0 sent for <c>curl_mime_filedata("nope.txt")</c>
/// followed by a data part (BL-3210's case 1, 249 bytes), and the tool's <c>-F a=@nope.txt</c> still failing with
/// exit 26 before anything is sent (BL-3211).
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderMissingFileTests
{
    private const string Boundary = "------------------------AbCdEfGhIjKlMnOpQrStUv";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MissingFileThatMaySendEmpty_IsSentAsAnEmptyPartWithNoFileNameMatchingLibcurl()
    {
        MultipartFormBuildResult result = await BuildAsync(
            MissingFileSystem(),
            FilePart(sendsEmptyWhenMissing: true),
            new MultipartFormPart("b", MultipartFormPartKind.Text, "hello", null, null, NoHeaders, NoParts));

        string expected =
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n\r\n"
            + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\nhello\r\n--{Boundary}--\r\n";
        Assert.IsTrue(result.IsBuilt);
        Assert.AreEqual(249, result.Body!.Length);
        Assert.AreEqual(expected, await ReadAsync(result));
    }

    [TestMethod]
    public async Task MissingFileThatMaySendEmpty_WithAnEncoder_IsSentAsAnEmptyEncodedPart()
    {
        MultipartFormBuildResult result = await BuildAsync(MissingFileSystem(), FilePart(sendsEmptyWhenMissing: true) with { Encoder = "base64" });

        Assert.IsTrue(result.IsBuilt);
        Assert.AreEqual(
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Transfer-Encoding: base64\r\n\r\n\r\n--{Boundary}--\r\n",
            await ReadAsync(result));
    }

    [TestMethod]
    public async Task MissingFileFromTheTool_FailsWithReadErrorBeforeSending()
    {
        MultipartFormBuildResult result = await BuildAsync(MissingFileSystem(), FilePart(sendsEmptyWhenMissing: false));

        Assert.IsFalse(result.IsBuilt);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(MultipartFormBodyBuilder.OpenFailedMessage, result.Failure.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task DirectoryThatMaySendEmptyWhenMissing_StillFailsWithReadError()
    {
        MultipartFormBuildResult result = await BuildAsync(
            new FormFileSystem().WithFailure("nope.txt", FileAccessStatus.IsDirectory),
            FilePart(sendsEmptyWhenMissing: true));

        Assert.IsFalse(result.IsBuilt);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
    }

    private static FormFileSystem MissingFileSystem() => new FormFileSystem().WithFailure("nope.txt", FileAccessStatus.NotFound);

    private static MultipartFormPart FilePart(bool sendsEmptyWhenMissing) =>
        new("a", MultipartFormPartKind.FileUpload, "nope.txt", null, "nope.txt", NoHeaders, NoParts)
        {
            SendsEmptyWhenMissing = sendsEmptyWhenMissing,
        };

    private async Task<MultipartFormBuildResult> BuildAsync(FormFileSystem files, params MultipartFormPart[] parts)
    {
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, () => Boundary, standardInput: null, UnseekableFileLength.Unknown);
        return await builder.BuildAsync(parts, TestContext.CancellationToken);
    }

    private async Task<string> ReadAsync(MultipartFormBuildResult result)
    {
        using MemoryStream copy = new();
        await result.Body!.Content.CopyToAsync(copy, TestContext.CancellationToken);
        await result.Body!.Content.DisposeAsync();
        return Encoding.UTF8.GetString(copy.ToArray());
    }
}
