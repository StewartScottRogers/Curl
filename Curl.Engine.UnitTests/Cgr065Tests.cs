using System.Text;
using Curl.Api.Options.Http;
using Curl.Cookies;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-065 (BL-3594): a <c>-b name=value</c> string is sent as its bytes in the command line's
/// encoding, one character per byte as the stored cookies are, as curl copies it from its <c>argv</c>
/// (measured on curl 8.18.0, Linux: <c>-b c=中</c> sends <c>Cookie: c=E4 B8 AD</c>).
/// </summary>
[TestClass]
public sealed class Cgr065Tests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://127.0.0.1/a");

    [TestMethod]
    [DataRow("c=中", "c=\u00E4\u00B8\u00AD", DisplayName = "c=中")]
    [DataRow("c=é", "c=\u00C3\u00A9", DisplayName = "c=é")]
    public void HandlerStore_CookieStringWithUtf8Encoding_GivesItsUtf8Bytes(string cookie, string expected)
    {
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = cookie }, new CookieStore(), Encoding.UTF8)!;

        string? header = cookies.HandlerStore.GetCookieHeader(Url, false, DateTimeOffset.UnixEpoch, NoTransferEvents.Instance);

        Assert.AreEqual(expected, header);
    }

    [TestMethod]
    public void HandlerStore_NoEncodingNamed_GivesTheStringAsItIs()
    {
        CookieEngine cookies = CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = "c=é" }, new CookieStore())!;

        string? header = cookies.HandlerStore.GetCookieHeader(Url, false, DateTimeOffset.UnixEpoch, NoTransferEvents.Instance);

        Assert.AreEqual("c=é", header);
    }

    [TestMethod]
    public void CookieLineTooLong_CountsTheBytesInTheNamedEncoding()
    {
        string cookie = "c=" + new string('é', 4100);

        Assert.IsTrue(CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = cookie }, new CookieStore(), Encoding.UTF8)!.CookieLineTooLong);
        Assert.IsFalse(CookieEngine.FromHttpOptions(new EasyHttpOptions { Cookie = cookie }, new CookieStore(), Encoding.Latin1)!.CookieLineTooLong);
    }
}
