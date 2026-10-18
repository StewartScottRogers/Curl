using CurlHttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Api.HttpMessageHandler.Core;

[TestClass]
public sealed class HttpRequestMappingTests
{
    [TestMethod]
    [DataRow(1, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http10)]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http11)]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http11)]
    [DataRow(1, 1, HttpVersionPolicy.RequestVersionOrHigher, CurlHttpVersion.None)]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http2)]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionOrHigher, CurlHttpVersion.Http2)]
    [DataRow(2, 0, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http2PriorKnowledge)]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionOrLower, CurlHttpVersion.Http3)]
    [DataRow(3, 0, HttpVersionPolicy.RequestVersionExact, CurlHttpVersion.Http3Only)]
    public void VersionOf_MapsVersionAndPolicy(int major, int minor, HttpVersionPolicy policy, CurlHttpVersion expected)
    {
        Assert.AreEqual(expected, HttpRequestMapping.VersionOf(new Version(major, minor), policy));
    }

    [TestMethod]
    public async Task ToCurlRequestAsync_NoUri_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => HttpRequestMapping.ToCurlRequestAsync(new HttpRequestMessage(), null, CancellationToken.None));
    }

    [TestMethod]
    public async Task ToCurlRequestAsync_NoContent_HasNoBodyAndOnlyTheRequestHeaders()
    {
        HttpRequestMessage request = new(HttpMethod.Delete, "http://example/x");
        request.Headers.Add("X-A", "1");

        CurlRequest mapped = await HttpRequestMapping.ToCurlRequestAsync(request, null, CancellationToken.None);

        Assert.AreEqual(HttpMethod.Delete, mapped.Method);
        Assert.IsNull(mapped.Body);
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("X-A", "1") }, mapped.Headers.ToArray());
        Assert.AreEqual(CurlHttpVersion.Http11, mapped.Http!.HttpVersion);
    }
}
