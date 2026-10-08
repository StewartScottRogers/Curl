using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that an <see cref="HttpAuthRequest" /> carries every member ADR-0014 gives it, and
/// compares by value.
/// </summary>
[TestClass]
public sealed class HttpAuthRequestTests
{
    private static readonly CurlUrl OriginUrl = CurlUrl.Parse("http://example.com/path?q=1");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsEveryMember()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var credential = new NetworkCredential("u", "p");
        diagnostics.Arrange("method", "POST");
        diagnostics.Arrange("request target", "/path?q=1");
        diagnostics.Arrange("credential user", credential.UserName);
        diagnostics.Arrange("bearer token", "token");

        var request = new HttpAuthRequest(
            "POST", OriginUrl, "/path?q=1", credential, "token", HttpAuthSchemes.Any, IsProxy: false);

        diagnostics.Act("request", request);
        diagnostics.Assert("method", "POST", request.Method);
        diagnostics.Assert("allowed schemes", HttpAuthSchemes.Any, request.AllowedSchemes);
        diagnostics.Assert("is proxy", false, request.IsProxy);
        Assert.AreEqual("POST", request.Method);
        Assert.AreSame(OriginUrl, request.Url);
        Assert.AreEqual("/path?q=1", request.RequestTarget);
        Assert.AreSame(credential, request.Credential);
        Assert.AreEqual("token", request.BearerToken);
        Assert.AreEqual(HttpAuthSchemes.Any, request.AllowedSchemes);
        Assert.IsFalse(request.IsProxy);
    }

    [TestMethod]
    public void ServerCertificate_EmptyByDefaultAndKeptWhenSet()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var request = new HttpAuthRequest(
            "GET", OriginUrl, "/path?q=1", null, null, HttpAuthSchemes.Negotiate, IsProxy: false);
        byte[] certificate = [0x30, 0x00];
        diagnostics.Arrange("request", request);
        diagnostics.Bytes("certificate", certificate);

        byte[] kept = (request with { ServerCertificate = certificate }).ServerCertificate.ToArray();

        diagnostics.Act("default certificate is empty", request.ServerCertificate.IsEmpty);
        diagnostics.Bytes("kept certificate", kept);
        diagnostics.Diff("kept certificate", certificate, kept);
        diagnostics.Assert("default certificate is empty", true, request.ServerCertificate.IsEmpty);
        Assert.IsTrue(request.ServerCertificate.IsEmpty);
        CollectionAssert.AreEqual(certificate, (request with { ServerCertificate = certificate }).ServerCertificate.ToArray());
    }

    [TestMethod]
    public void Equals_ForTheSameRequestToAProxy_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var toOrigin = new HttpAuthRequest(
            "GET", OriginUrl, "/path?q=1", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        var toProxy = toOrigin with { IsProxy = true };
        diagnostics.Arrange("to origin", toOrigin);
        diagnostics.Arrange("to proxy", toProxy);

        bool equal = toOrigin.Equals(toProxy);

        diagnostics.Act("equal", equal);
        diagnostics.Assert("equal", false, equal);
        Assert.AreNotEqual(toOrigin, toProxy);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var newUrl = CurlUrl.Parse("http://proxy.example:3128/");
        var newCredential = new NetworkCredential("proxyuser", "proxypass");
        var original = new HttpAuthRequest(
            "GET", OriginUrl, "/path?q=1", null, "token", HttpAuthSchemes.Bearer, IsProxy: false);
        diagnostics.Arrange("original", original);
        diagnostics.Arrange("new url", newUrl);
        diagnostics.Arrange("new credential user", newCredential.UserName);

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

        diagnostics.Act("copy", copy);
        diagnostics.Act("original after copy", original);
        diagnostics.Assert("copy method", "CONNECT", copy.Method);
        diagnostics.Assert("original method", "GET", original.Method);
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
