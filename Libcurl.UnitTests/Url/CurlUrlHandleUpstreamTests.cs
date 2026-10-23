using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Engine;
using Libcurl.Url;

namespace Libcurl.UnitTests.Url;

/// <summary>
/// Pins the public URL handle over the managed engine to upstream curl 8.22.0's <c>curl_url_set</c> and
/// <c>curl_url_get</c> for cases (1), (4) and (8) of report 26 (BL-3197).
/// </summary>
[TestClass]
public sealed class CurlUrlHandleUpstreamTests
{
    [TestMethod]
    public void Set_EmptyUserUnderUrlEncode_LeavesTheUserUnset()
    {
        using CurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.User, string.Empty, CurlUrlFlags.UrlEncode));

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text));
        Assert.AreEqual("http://a/", text);
        Assert.AreEqual(CurlUrlCode.NoUser, url.Get(CurlUrlPart.User, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public void Set_HostWithASpaceUnderUrlEncode_IsBadHostname()
    {
        using CurlUrlHandle url = Parsed("http://a/");

        CurlUrlCode code = url.Set(CurlUrlPart.Host, "a b", CurlUrlFlags.UrlEncode);

        Assert.AreEqual(CurlUrlCode.BadHostname, code);
        Assert.AreEqual(21, (int)code);
    }

    [TestMethod]
    public void Set_UrlWithAMalformedPort_IsBadPortNumberWithUpstreamsText()
    {
        using CurlUrlHandle url = new(new ManagedCurlUrlHandle());

        CurlUrlCode code = url.Set(CurlUrlPart.Url, "http://a:12x/", CurlUrlFlags.None);

        Assert.AreEqual(CurlUrlCode.BadPortNumber, code);
        Assert.AreEqual("Port number was not a decimal number between 0 and 65535", CurlUrlHandle.ErrorText(code));
    }

    private static CurlUrlHandle Parsed(string text)
    {
        CurlUrlHandle url = new(new ManagedCurlUrlHandle());
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));

        return url;
    }
}
