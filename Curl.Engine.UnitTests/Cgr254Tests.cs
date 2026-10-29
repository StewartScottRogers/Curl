using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>
/// CGR-254: a relative URL set under <c>CURLU_PATH_AS_IS</c> is still resolved against the URL held, so its
/// dot segments go, as libcurl 8.22.0's <c>redirect_url</c> removes them before the set.
/// </summary>
[TestClass]
public sealed class Cgr254Tests
{
    [TestMethod]
    public void Set_RelativeUrlWithPathAsIs_ResolvesTheDotSegments()
    {
        using ManagedCurlUrlHandle url = new();
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "http://a/b/c/d", CurlUrlFlags.None));

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "../g/./h", CurlUrlFlags.PathAsIs));

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text));
        Assert.AreEqual("http://a/b/g/h", text);
    }
}
