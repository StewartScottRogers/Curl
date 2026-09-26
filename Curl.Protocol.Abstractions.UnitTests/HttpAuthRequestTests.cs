using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that an <see cref="HttpAuthRequest" /> carries every member ADR-0014 gives it, and
/// compares by value.
/// </summary>
[TestClass]
public sealed class HttpAuthRequestTests
{
    private static readonly Uri OriginUrl = new("http://example.com/path?q=1");

    [TestMethod]
    public void Constructor_RoundTripsEveryMember()
    {
        var credential = new NetworkCredential("u", "p");

        var request = new HttpAuthRequest(
            "POST", OriginUrl, "/path?q=1", credential, "token", HttpAuthSchemes.Any, IsProxy: false);

        Assert.AreEqual("POST", request.Method);
        Assert.AreSame(OriginUrl, request.Url);
        Assert.AreEqual("/path?q=1", request.RequestTarget);
        Assert.AreSame(credential, request.Credential);
        Assert.AreEqual("token", request.BearerToken);
        Assert.AreEqual(HttpAuthSchemes.Any, request.AllowedSchemes);
        Assert.IsFalse(request.IsProxy);
    }

    [TestMethod]
    public void Equals_ForTheSameRequestToAProxy_ReturnsFalse()
    {
        var toOrigin = new HttpAuthRequest(
            "GET", OriginUrl, "/path?q=1", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        var toProxy = toOrigin with { IsProxy = true };

        Assert.AreNotEqual(toOrigin, toProxy);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        var newUrl = new Uri("http://proxy.example:3128/");
        var newCredential = new NetworkCredential("proxyuser", "proxypass");
        var original = new HttpAuthRequest(
            "GET", OriginUrl, "/path?q=1", null, "token", HttpAuthSchemes.Bearer, IsProxy: false);

        var copy = original with
        {
            Method = "CONNECT",
            Url = newUrl,
            RequestTarget = "example.com:443",
            Credential = newCredential,
            BearerToken = null,
            AllowedSchemes = HttpAuthSchemes.Basic,
            IsProxy = true,
        };

        Assert.AreEqual("CONNECT", copy.Method);
        Assert.AreSame(newUrl, copy.Url);
        Assert.AreEqual("example.com:443", copy.RequestTarget);
        Assert.AreSame(newCredential, copy.Credential);
        Assert.IsNull(copy.BearerToken);
        Assert.AreEqual(HttpAuthSchemes.Basic, copy.AllowedSchemes);
        Assert.IsTrue(copy.IsProxy);
        Assert.AreEqual("GET", original.Method);
        Assert.AreSame(OriginUrl, original.Url);
        Assert.AreEqual("/path?q=1", original.RequestTarget);
        Assert.IsNull(original.Credential);
        Assert.AreEqual("token", original.BearerToken);
        Assert.AreEqual(HttpAuthSchemes.Bearer, original.AllowedSchemes);
        Assert.IsFalse(original.IsProxy);
    }
}
