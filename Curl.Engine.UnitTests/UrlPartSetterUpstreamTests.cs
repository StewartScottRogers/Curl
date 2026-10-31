using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>
/// Pins the URL handle's set and get to upstream curl 8.22.0's <c>curl_url_set</c> and
/// <c>curl_url_get</c> for the cases of report 26 (BL-3197): each URL text and <see cref="CurlUrlCode" />
/// is the one upstream's <c>lib/urlapi.c</c> gives.
/// </summary>
[TestClass]
public sealed class UrlPartSetterUpstreamTests
{
    [TestMethod]
    [DataRow(CurlUrlPart.User, CurlUrlCode.NoUser)]
    [DataRow(CurlUrlPart.Password, CurlUrlCode.NoPassword)]
    [DataRow(CurlUrlPart.Options, CurlUrlCode.NoOptions)]
    [DataRow(CurlUrlPart.ZoneId, CurlUrlCode.NoZoneId)]
    public void Set_EmptyLoginPartOrZoneUnderUrlEncode_LeavesThePartUnset(CurlUrlPart part, CurlUrlCode missing)
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(part, string.Empty, CurlUrlFlags.UrlEncode));

        Assert.AreEqual("http://a/", Got(url, CurlUrlPart.Url));
        Assert.AreEqual(missing, url.Get(part, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public void Set_EmptyUserWithoutUrlEncode_KeepsTheEmptyUser()
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.User, string.Empty, CurlUrlFlags.None));

        Assert.AreEqual("http://@a/", Got(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_EmptyFragment_KeepsTheHashAndAnEmptyFragment()
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Fragment, string.Empty, CurlUrlFlags.None));

        Assert.AreEqual("http://a/#", Got(url, CurlUrlPart.Url));
        Assert.AreEqual(string.Empty, Got(url, CurlUrlPart.Fragment));
    }

    [TestMethod]
    [DataRow("a%b")]
    [DataRow("a%")]
    public void Set_FragmentEndingInAPercentAndAtMostOneCharacter_StoresTheTextAsGiven(string fragment)
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Fragment, fragment, CurlUrlFlags.None));

        Assert.AreEqual(fragment, Got(url, CurlUrlPart.Fragment));
    }

    [TestMethod]
    public void Set_EmptyFragmentUnderUrlEncode_LeavesTheFragmentUnset()
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/#f");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Fragment, string.Empty, CurlUrlFlags.UrlEncode));

        Assert.AreEqual("http://a/", Got(url, CurlUrlPart.Url));
        Assert.AreEqual(CurlUrlCode.NoFragment, url.Get(CurlUrlPart.Fragment, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public void SetUrl_RelativeToAnUnsupportedSchemeWithoutNonSupportScheme_IsUnsupportedScheme()
    {
        using ManagedCurlUrlHandle url = new();
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "foo://a/", CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual(CurlUrlCode.UnsupportedScheme, url.Set(CurlUrlPart.Url, "b", CurlUrlFlags.None));
    }

    [TestMethod]
    public void Set_EmptyQueryUnderAppendQuery_AppendsAnAmpersand()
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/?x=1");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Query, string.Empty, CurlUrlFlags.AppendQuery));

        Assert.AreEqual("http://a/?x=1&", Got(url, CurlUrlPart.Url));
    }

    [TestMethod]
    [DataRow("a b")]
    [DataRow("[::1]")]
    [DataRow("a/b")]
    [DataRow("a?b")]
    [DataRow("a#b")]
    [DataRow("hé")]
    public void Set_HostUnderUrlEncodeThatNeedsEncoding_IsBadHostname(string host)
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.BadHostname, url.Set(CurlUrlPart.Host, host, CurlUrlFlags.UrlEncode));

        Assert.AreEqual("http://a/", Got(url, CurlUrlPart.Url));
    }

    [TestMethod]
    [DataRow(CurlUrlFlags.None)]
    [DataRow(CurlUrlFlags.UrlEncode)]
    public void Set_HostLongerThan255Characters_IsBadHostname(CurlUrlFlags flags)
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.BadHostname, url.Set(CurlUrlPart.Host, new string('a', 256), flags));
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, new string('a', 255), flags));
    }

    [TestMethod]
    public void Set_UpperCaseScheme_KeepsTheCaseGiven()
    {
        using ManagedCurlUrlHandle url = Parsed("http://a/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Scheme, "HTTPS", CurlUrlFlags.None));

        Assert.AreEqual("HTTPS", Got(url, CurlUrlPart.Scheme));
        Assert.AreEqual("HTTPS://a/", Got(url, CurlUrlPart.Url));
        Assert.AreEqual("443", Got(url, CurlUrlPart.Port, CurlUrlFlags.DefaultPort));
    }

    [TestMethod]
    [DataRow("http:a")]
    [DataRow(@"http:\\a\b")]
    public void SetUrl_SchemeWithoutSlashes_IsBadSlashes(string text)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadSlashes, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));
    }

    [TestMethod]
    [DataRow("http://a:12x/")]
    [DataRow("http://a:99999/")]
    [DataRow("http://a:-1/")]
    [DataRow("http://a:+1/")]
    [DataRow("http://a:0x10/")]
    [DataRow("http://a:1.5/")]
    [DataRow("http://a:%31/")]
    [DataRow("http://a:1x")]
    public void SetUrl_MalformedPort_IsBadPortNumber(string text)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadPortNumber, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));
    }

    private static ManagedCurlUrlHandle Parsed(string text)
    {
        ManagedCurlUrlHandle url = new();
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));

        return url;
    }

    private static string? Got(ManagedCurlUrlHandle url, CurlUrlPart part, CurlUrlFlags flags = CurlUrlFlags.None)
    {
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(part, flags, out string? value));

        return value;
    }
}
