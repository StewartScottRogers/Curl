using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class ExchangeKeyTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/r");

    [TestMethod]
    public void For_AMethodUrlAndBody_IsTheLowercaseSha256OfTheLineAndBody_Adr1129Section2()
    {
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes("POST http://example.com/r\na=1")));

        var key = ExchangeKey.For("POST", "http://example.com/r", "a=1"u8);

        Assert.AreEqual(expected, key);
        Assert.AreNotEqual(key, ExchangeKey.For("POST", "http://example.com/r", "a=2"u8));
    }

    [TestMethod]
    public void MethodOf_EachRequestShape_GivesTheMethodItSends_Adr1129Section2()
    {
        Assert.AreEqual("DELETE", ExchangeKey.MethodOf(Context(new HttpRequestOptions { CustomMethod = "DELETE" })));
        Assert.AreEqual("HEAD", ExchangeKey.MethodOf(new TransferContext { Url = Url, Output = new MemoryStream(), Http = new HttpRequestOptions(), NoBody = true }));
        Assert.AreEqual("POST", ExchangeKey.MethodOf(new TransferContext { Url = Url, Output = new MemoryStream(), Http = new HttpRequestOptions(), PostData = new byte[] { 1 } }));
        Assert.AreEqual("POST", ExchangeKey.MethodOf(new TransferContext { Url = Url, Output = new MemoryStream(), Http = new HttpRequestOptions(), PostWithoutData = true }));
        Assert.AreEqual("POST", ExchangeKey.MethodOf(Context(new HttpRequestOptions { Body = new BytesBody(new byte[] { 1 }, "text/plain") })));
        Assert.AreEqual("PUT", ExchangeKey.MethodOf(new TransferContext { Url = Url, Output = new MemoryStream(), Http = new HttpRequestOptions(), Upload = new MemoryStream() }));
        Assert.AreEqual("GET", ExchangeKey.MethodOf(Context()));
        Assert.AreEqual("GET", ExchangeKey.MethodOf(new TransferContext { Url = Url, Output = new MemoryStream() }));
        Assert.ThrowsExactly<ArgumentNullException>(() => ExchangeKey.MethodOf(null!));
    }

    private static TransferContext Context(HttpRequestOptions? http = null) => new()
    {
        Url = Url,
        Output = new MemoryStream(),
        Http = http ?? new HttpRequestOptions(),
    };
}
