using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Checks <see cref="HandshakeHeaderFields"/> against how curl sends its options on HTTP.</summary>
[TestClass]
public sealed class HandshakeHeaderFieldsTests
{
    [TestMethod]
    public void Of_NoOptions_SendsOnlyCurlsUserAgent() =>
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("User-Agent", "curl/8.22.0") },
            HandshakeHeaderFields.Of(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream() }).ToArray());

    [TestMethod]
    public void Of_TheOptions_SendsThemInOrder()
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = "TT", UserAgent = "agentX", CookieStrings = "c=1", Headers = ["X-A: 1"] },
        };

        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Authorization", "Bearer TT"), new("User-Agent", "agentX"), new("Cookie", "c=1"), new("X-A", "1") },
            HandshakeHeaderFields.Of(context).ToArray());
    }

    [TestMethod]
    public void Build_UserAndPassword_SendsBasic() =>
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Authorization", "Basic dTpw"), new("User-Agent", "a") },
            HandshakeHeaderFields.Build(null, new NetworkCredential("u", "p"), "a", null, []).ToArray());

    [TestMethod]
    public void Build_EmptyUserAgent_SendsNone() =>
        Assert.AreEqual(0, HandshakeHeaderFields.Build(null, null, string.Empty, string.Empty, []).Count);

    [TestMethod]
    public void Build_CustomHeaders_ReplaceRemoveAndSendEmpty() =>
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Authorization", "Bearer abc"), new("X-Empty", string.Empty) },
            HandshakeHeaderFields.Build(null, null, null, null, ["Authorization: Bearer abc", "User-Agent:", "X-Empty;", "no-colon", ";", "X-Inj: a\r\nEvil: b"]).Where(f => f.Key != "X-Inj").ToArray());
}
