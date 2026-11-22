using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins AF-0982: a form file that shrank before the body is built (a trace file truncated it) is still declared at the
/// size it had before, so the send reads it short and fails with exit 26 as curl 8.22.0's does.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderTraceSizeTests
{
    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(10L, 10L, DisplayName = "shrunk file keeps its earlier size")]
    [DataRow(1L, 2L, DisplayName = "file that is larger now keeps its size")]
    [DataRow(2L, 2L, DisplayName = "file of the same size keeps its size")]
    public async Task BuildAsync_FileSizedBeforeTrace_DeclaresTheLargerSize(long sizedBefore, long expectedFileLength)
    {
        long withoutSizes = (await BuildAsync(null)).Body!.Length!.Value;

        long withSizes = (await BuildAsync(new Dictionary<string, long> { ["f.txt"] = sizedBefore })).Body!.Length!.Value;

        Assert.AreEqual(withoutSizes - 2 + expectedFileLength, withSizes);
    }

    [TestMethod]
    public async Task BuildAsync_OtherFileSizedBeforeTrace_DeclaresTheFileAsItIs()
    {
        long withoutSizes = (await BuildAsync(null)).Body!.Length!.Value;

        long withSizes = (await BuildAsync(new Dictionary<string, long> { ["g.txt"] = 99 })).Body!.Length!.Value;

        Assert.AreEqual(withoutSizes, withSizes);
    }

    private async Task<MultipartFormBuildResult> BuildAsync(IReadOnlyDictionary<string, long>? sizes)
    {
        MultipartFormBodyBuilder builder = new(new FormFileSystem().WithFile("f.txt", "ab"), Encoding.UTF8, () => "B", standardInput: null)
        {
            FileSizesBeforeTrace = sizes,
        };

        return await builder.BuildAsync(
            [new MultipartFormPart("f", MultipartFormPartKind.FileUpload, "f.txt", null, null, [], NoParts)],
            TestContext.CancellationToken);
    }
}
