using System.Net;
using System.Text;

namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class GeneratedRequestTests
{
    [TestMethod]
    public void From_ATransferWithNoHttpOptions_IsAGetOfTheUrlAsSent()
    {
        var request = GeneratedRequest.From(Context("HTTP://Example.com:8080/a/../b?q=1#frag"));

        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("http://Example.com:8080/b?q=1", request.Url);
        Assert.IsEmpty(request.Headers);
        Assert.IsNull(request.Body);
        Assert.IsNull(request.BodyText);
        Assert.IsFalse(request.FollowRedirects);
        Assert.IsFalse(request.Compressed);
    }

    [TestMethod]
    public void From_AnIpv6Host_IsBracketed()
    {
        Assert.AreEqual("https://[::1]/", GeneratedRequest.From(Context("https://[::1]/")).Url);
    }

    [TestMethod]
    public void From_EachWayCurlChoosesTheMethod_GivesThatMethod()
    {
        Assert.AreEqual("POST", GeneratedRequest.From(Context(http: new HttpRequestOptions { Body = new BytesBody("a=1"u8.ToArray(), "application/x-www-form-urlencoded") })).Method);
        Assert.AreEqual("POST", GeneratedRequest.From(Context(postData: "x"u8.ToArray())).Method);
        Assert.AreEqual("POST", GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, PostWithoutData = true }).Method);
        Assert.AreEqual("PUT", GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, Upload = Stream.Null }).Method);
        Assert.AreEqual("HEAD", GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, NoBody = true }).Method);
        Assert.AreEqual("PATCH", GeneratedRequest.From(Context(http: new HttpRequestOptions { CustomMethod = "PATCH" })).Method);
    }

    [TestMethod]
    public void From_TheBody_IsCurlsBytesWithItsContentType()
    {
        var bytes = GeneratedRequest.From(Context(http: new HttpRequestOptions { Body = new BytesBody("a=1"u8.ToArray(), "text/x") }));
        var postData = GeneratedRequest.From(Context(postData: "b"u8.ToArray()));
        var stream = GeneratedRequest.From(Context(http: new HttpRequestOptions { Body = new StreamBody(Stream.Null, null, "multipart/form-data") }));

        Assert.AreEqual("a=1", bytes.BodyText);
        Assert.AreEqual(new KeyValuePair<string, string>("Content-Type", "text/x"), bytes.Headers.Single());
        Assert.AreEqual("b", postData.BodyText);
        Assert.AreEqual("application/x-www-form-urlencoded", postData.ContentHeaders.Single().Value);
        Assert.IsNull(stream.Body);
        Assert.AreEqual("POST", stream.Method);
    }

    [TestMethod]
    public void BodyText_BytesThatAreNotUtf8_IsNull()
    {
        Assert.IsNull(GeneratedRequest.From(Context(postData: [0xff, 0xfe])).BodyText);
    }

    [TestMethod]
    public void From_TheOptionsThatSetFields_GiveThoseFieldsUnlessHGaveThem()
    {
        var http = new HttpRequestOptions { UserAgent = "agent", Referer = "https://r/", BearerToken = "tok", Headers = ["Accept: */*", "X-Empty;", "Host:", "NoSeparator"] };
        var request = GeneratedRequest.From(Context(http: http));

        CollectionAssert.AreEqual(
            new[] { "User-Agent=agent", "Referer=https://r/", "Authorization=Bearer tok", "Accept=*/*", "X-Empty=" },
            request.Headers.Select(header => header.Key + "=" + header.Value).ToArray());
        Assert.AreEqual(5, request.RequestHeaders.Count());
    }

    [TestMethod]
    public void From_HGivesAField_ItReplacesTheOneCurlWouldAdd()
    {
        var http = new HttpRequestOptions { UserAgent = "agent", Body = new BytesBody("x"u8.ToArray(), "text/x"), Headers = ["user-agent: mine", "Content-Type:"] };
        var request = GeneratedRequest.From(Context(http: http));

        Assert.AreEqual("user-agent=mine", string.Join(",", request.Headers.Select(header => header.Key + "=" + header.Value)));
    }

    // RFC 7617 section 2: Basic credentials are base64 of user-id ":" password.
    [TestMethod]
    public void From_Credentials_GiveBasicAuthorization()
    {
        var request = GeneratedRequest.From(new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, Credentials = new NetworkCredential("Aladdin", "open sesame") });

        Assert.AreEqual("Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==", request.Headers.Single(header => header.Key == "Authorization").Value);
    }

    [TestMethod]
    public void From_RedirectAndDecompressionOptions_AreKept()
    {
        var request = GeneratedRequest.From(Context(http: new HttpRequestOptions { FollowRedirects = true, MaxRedirects = 3, Compressed = true }));

        Assert.IsTrue(request.FollowRedirects);
        Assert.AreEqual(3, request.MaxRedirects);
        Assert.IsTrue(request.Compressed);
    }

    private static TransferContext Context(string url = "http://h/", HttpRequestOptions? http = null, byte[]? postData = null) =>
        new() { Url = CurlUrl.Parse(url), Output = Stream.Null, Http = http, PostData = postData is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(postData) };

    internal static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
