using System.Net;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport.Session;

/// <summary>Checks <see cref="WebTransportConnectHeaderFields"/> against how curl sends its options on HTTP, in HTTP/3's lower case.</summary>
[TestClass]
public sealed class WebTransportConnectHeaderFieldsTests
{
    [TestMethod]
    public void Of_NoOptions_SendsOnlyCurlsUserAgent() =>
        CollectionAssert.AreEqual(
            new HeaderField[] { new("user-agent", "curl/8.22.0") },
            WebTransportConnectHeaderFields.Of(new TransferContext { Url = CurlUrl.Parse("webtransport://example.com/"), Output = new MemoryStream() }).ToArray());

    [TestMethod]
    public void Of_TheOptions_SendsThemInOrderInLowerCase()
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("webtransport://example.com/"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = "t", UserAgent = "agW", CookieStrings = "c=1", Headers = ["X-A: 1", "Authorization: Bearer t2"] },
        };

        CollectionAssert.AreEqual(
            new HeaderField[] { new("user-agent", "agW"), new("cookie", "c=1"), new("x-a", "1"), new("authorization", "Bearer t2") },
            WebTransportConnectHeaderFields.Of(context).ToArray());
    }

    [TestMethod]
    public void Build_BearerBeatsBasic_AndBasicIsBase64OfUserAndPassword()
    {
        Assert.AreEqual(new HeaderField("authorization", "Bearer T"), WebTransportConnectHeaderFields.Build("T", new NetworkCredential("u", "p"), "a", null, [])[0]);
        Assert.AreEqual(new HeaderField("authorization", "Basic dTpw"), WebTransportConnectHeaderFields.Build(null, new NetworkCredential("u", "p"), "a", null, [])[0]);
    }

    [TestMethod]
    public void Build_EmptyUserAgentAndCookies_SendsNeither() =>
        Assert.AreEqual(0, WebTransportConnectHeaderFields.Build(null, null, string.Empty, string.Empty, []).Count);

    [TestMethod]
    public void Build_CustomHeaders_ColonRemovesSemicolonSendsEmptyAndOnlyTheFirstLineCounts() =>
        CollectionAssert.AreEqual(
            new HeaderField[] { new("x-empty", string.Empty), new("x-inj", "a") },
            WebTransportConnectHeaderFields.Build(null, null, null, null, ["User-Agent:", "X-Empty;", "no-colon", ";", "X-Inj: a\r\nEvil: b"]).ToArray());

    [TestMethod]
    [DataRow(":path: /x")]
    [DataRow(":method: GET")]
    [DataRow("Host: evil.example")]
    [DataRow("Connection: close")]
    [DataRow("Transfer-Encoding: chunked")]
    [DataRow("Host;")]
    [DataRow(":path;")]
    public void Build_PseudoHeaderHostOrConnectionSpecificField_IsNeverAdded(string header) =>
        CollectionAssert.AreEqual(
            new HeaderField[] { new("user-agent", "curl/8.22.0") },
            WebTransportConnectHeaderFields.Build(null, null, null, null, [header]).ToArray());
}
