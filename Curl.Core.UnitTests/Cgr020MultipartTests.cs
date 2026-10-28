using System.Text;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-020 (BL-3832): a multipart part header holding the U+DC80-U+DCFF characters that
/// stand for header-file bytes UTF-8 could not read is written as those bytes, as curl does.
/// </summary>
[TestClass]
public sealed class Cgr020MultipartTests
{
    [TestMethod]
    public async Task BuildAsync_PartHeaderWithEscapedBytes_WritesThoseBytes()
    {
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "b", null, null, ["X-C: \uDCFF\uDC80\uDCC3(𐂀"], []);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, () => "B");

        MultipartFormBuildResult result = await builder.BuildAsync([part], CancellationToken.None);

        using MemoryStream copy = new();
        await result.Body!.Content.CopyToAsync(copy);
        byte[] expected = [.. "X-C: "u8, 0xFF, 0x80, 0xC3, (byte)'(', 0xF0, 0x90, 0x82, 0x80];
        CollectionAssert.AreEqual(expected, copy.ToArray().AsSpan().ToArray().Skip(IndexOf(copy.ToArray(), "X-C: "u8)).Take(expected.Length).ToArray());
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle);
}
