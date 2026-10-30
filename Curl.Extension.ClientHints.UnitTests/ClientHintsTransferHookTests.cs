using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ClientHints.UnitTests;

[TestClass]
public sealed class ClientHintsTransferHookTests
{
    private readonly ITransferHook _hook = new ClientHintsTransferHook("Windows");

    [TestMethod]
    public void RequestHeadersFor_AnHttpsUrl_SendsTheLowEntropyHintsAsStructuredFields_WicgUaChSections3Point1And3Point3()
    {
        var headers = _hook.RequestHeadersFor(Context("https://example.com/")).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Sec-CH-UA: \"curl\";v=\"8\", \"Not_A Brand\";v=\"99\"",
                "Sec-CH-UA-Mobile: ?0",
                "Sec-CH-UA-Platform: \"Windows\"",
            },
            headers);
    }

    [TestMethod]
    [DataRow("wss://example.com/")]
    [DataRow("HTTPS://example.com/")]
    [DataRow("http://localhost:8080/")]
    [DataRow("http://api.localhost/")]
    [DataRow("http://127.0.0.1/")]
    [DataRow("http://[::1]/")]
    public void RequestHeadersFor_APotentiallyTrustworthyUrl_SendsTheHints_WicgUaChSection2(string url)
    {
        Assert.HasCount(3, _hook.RequestHeadersFor(Context(url)).ToArray());
    }

    [TestMethod]
    [DataRow("http://example.com/")]
    [DataRow("http://192.0.2.1/")]
    [DataRow("ws://example.com/")]
    public void RequestHeadersFor_AnInsecureUrl_SendsNoHint_Rfc8942Section4Point1(string url)
    {
        Assert.IsEmpty(_hook.RequestHeadersFor(Context(url)).ToArray());
    }

    [TestMethod]
    public void RequestHeadersFor_AHintGivenWithH_LeavesItToH()
    {
        var context = Context("https://example.com/", new HttpRequestOptions { Headers = ["sec-ch-ua-mobile: ?1"] });

        var headers = _hook.RequestHeadersFor(context).ToArray();

        CollectionAssert.AreEqual(new[] { "Sec-CH-UA: " + ClientHintsTransferHook.Brands, "Sec-CH-UA-Platform: \"Windows\"" }, headers);
    }

    [TestMethod]
    public void RequestHeadersFor_NoHttpOptions_SendsTheHints()
    {
        Assert.HasCount(3, _hook.RequestHeadersFor(Context("https://example.com/", http: null)).ToArray());
    }

    private static TransferContext Context(string url) => Context(url, new HttpRequestOptions());

    private static TransferContext Context(string url, HttpRequestOptions? http) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Http = http,
    };
}
