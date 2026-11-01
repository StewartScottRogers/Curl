using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The 1 MiB request head limit. Measured on curl 8.22.0 with a <c>-K</c> file holding one
/// <c>header "X-Large: xxx..."</c> through <c>Record-CurlExchange.ps1</c> (BL-2167 Notes):
/// a head of 1,048,575 bytes is sent, one of 1,048,576 is refused with exit 100 and
/// <c>HTTP request too large</c> before a byte is sent (upstream test3229).
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    private const string LargeHeaderPrefix = "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nX-Large: ";

    [TestMethod]
    public void ThrowIfTooLarge_HeadOneByteUnderOneMebibyte_AcceptsIt()
    {
        HttpRequestOptions options = new() { Headers = [LargeHeader(HttpRequestHeadFormatter.MaximumRequestHeadLength - 1)] };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/"), options);
        HttpRequestHeadFormatter.ThrowIfTooLarge(head);

        Assert.HasCount(HttpRequestHeadFormatter.MaximumRequestHeadLength - 1, head);
    }

    [TestMethod]
    public void ThrowIfTooLarge_HeadOfOneMebibyte_ThrowsTooLarge()
    {
        HttpRequestOptions options = new() { Headers = [LargeHeader(HttpRequestHeadFormatter.MaximumRequestHeadLength)] };

        HttpTransferException refusal = Assert.ThrowsExactly<HttpTransferException>(() => HttpRequestHeadFormatter.ThrowIfTooLarge(HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/"), options)));

        Assert.AreEqual(CurlExitCode.TooLarge, refusal.ExitCode);
        Assert.AreEqual("HTTP request too large", refusal.Message);
        CollectionAssert.AreEqual(new[] { "HTTP request too large" }, refusal.InfoLines.ToArray());
    }

    [TestMethod]
    public void ThrowIfTooLarge_OneMebibyteHeaderValue_ThrowsTooLarge()
    {
        HttpRequestOptions options = new() { Headers = ["X-Large: " + new string('x', 1048576)] };

        HttpTransferException refusal = Assert.ThrowsExactly<HttpTransferException>(() => HttpRequestHeadFormatter.ThrowIfTooLarge(HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/"), options)));

        Assert.AreEqual(CurlExitCode.TooLarge, refusal.ExitCode);
    }

    /// <summary>The <c>X-Large</c> header that makes the whole head <paramref name="headLength" /> bytes.</summary>
    internal static string LargeHeader(int headLength) =>
        "X-Large: " + new string('x', headLength - LargeHeaderPrefix.Length - "\r\n\r\n".Length);
}
