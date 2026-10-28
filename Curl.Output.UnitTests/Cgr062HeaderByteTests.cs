using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-062 part 2 (BL-3867): curl 8.22.0 Schannel writes a raw byte 0x80 to 0xFF of an
/// absolute <c>Location</c> back as that one byte in <c>%{redirect_url}</c> and, once followed,
/// <c>%{url_effective}</c>. The HTTP handler and the follower carry those bytes marked
/// (<see cref="HeaderByteText"/>, ADR-0923); text from the command line, such as an IDN host a
/// relative <c>Location</c> keeps, stays UTF-8.
/// </summary>
[TestClass]
public sealed class Cgr062HeaderByteTests
{
    [TestMethod]
    public async Task RenderAsync_RedirectUrlHoldingAMarkedHeaderByte_WritesTheByte()
    {
        TransferInfo info = new() { EffectiveUrl = "http://127.0.0.1:1/a/", RedirectUrl = HeaderByteText.Mark("http://é.invalid/b") };

        byte[] written = await RenderAsync(info, "%{redirect_url}");

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("http://é.invalid/b"), written);
    }

    [TestMethod]
    public async Task RenderAsync_EffectiveUrlHoldingAMarkedHeaderByte_WritesTheByte()
    {
        TransferInfo info = new() { EffectiveUrl = HeaderByteText.Mark("http://é.invalid/b") };

        byte[] written = await RenderAsync(info, "%{url_effective}");

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("http://é.invalid/b"), written);
    }

    [TestMethod]
    public async Task RenderAsync_IdnHostFromTheCommandLine_WritesItAsUtf8()
    {
        TransferInfo info = new() { EffectiveUrl = "http://å.se/b", RedirectUrl = "http://å.se/b" };

        byte[] written = await RenderAsync(info, "%{url_effective} %{redirect_url}");

        CollectionAssert.AreEqual("http://å.se/b http://å.se/b"u8.ToArray(), written);
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartOfAMarkedEffectiveUrl_ParsesTheUnmarkedUrl()
    {
        TransferWriteOutVariables variables = new(new TransferInfo { EffectiveUrl = HeaderByteText.Mark("http://x.test/é") }, TransferResult.Success(0), "http://x.test/", 0);

        bool found = variables.TryGetVariableText("urle.host", out string? host);

        Assert.IsTrue(found);
        Assert.AreEqual("x.test", host);
    }

    private static async Task<byte[]> RenderAsync(TransferInfo info, string template)
    {
        WriteOutTemplateRenderer renderer = new(new NoFiles(), writesLineFeedAsCrLf: false, WriteOutTimeDialect.Glibc, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        await renderer.RenderAsync(template, new TransferWriteOutVariables(info, TransferResult.Success(0), "http://127.0.0.1:1/a/", 0), standardOutput, standardError);
        return standardOutput.ToArray();
    }

    private sealed class NoFiles : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }
    }
}
