using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="HttpRequestOptions" />: a new instance holds curl's defaults, and every
/// member set in the initializer reads back unchanged (ADR-0014).
/// </summary>
[TestClass]
public sealed class HttpRequestOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HttpRequestOptions_NothingSet_HoldsCurlsDefaults()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "new HttpRequestOptions()");

        var options = new HttpRequestOptions();

        diagnostics.Act("max redirects", options.MaxRedirects);
        diagnostics.Act("fail mode", options.Fail);
        diagnostics.Act("version", options.Version);
        diagnostics.Act("happy eyeballs timeout", options.HappyEyeballsTimeout);
        diagnostics.Act("continue wait", options.ContinueWait);
        diagnostics.Act("auth schemes", options.AuthSchemes);
        diagnostics.Assert("max redirects", 50, options.MaxRedirects);
        diagnostics.Assert("version", HttpVersionPreference.Http11, options.Version);
        diagnostics.Assert("auth schemes", HttpAuthSchemes.Basic, options.AuthSchemes);
        Assert.IsNull(options.CustomMethod);
        Assert.IsEmpty(options.Headers);
        Assert.IsEmpty(options.ProxyHeaders);
        Assert.IsNull(options.UserAgent);
        Assert.IsNull(options.Referer);
        Assert.IsNull(options.Body);
        Assert.IsFalse(options.FollowRedirects);
        Assert.AreEqual(50, options.MaxRedirects);
        Assert.AreEqual(0, options.RedirectsFollowed);
        Assert.AreEqual(HttpFailMode.None, options.Fail);
        Assert.AreEqual(HttpVersionPreference.Http11, options.Version);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), options.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(1), options.ContinueWait);
        Assert.AreEqual(HttpRequestOptions.DefaultContinueWait, options.ContinueWait);
        Assert.IsFalse(options.Compressed);
        Assert.IsFalse(options.TransferEncoding);
        Assert.IsFalse(options.Raw);
        Assert.IsFalse(options.IgnoreContentLength);
        Assert.IsNull(options.RequestTarget);
        Assert.AreEqual(HttpAuthSchemes.Basic, options.AuthSchemes);
        Assert.IsNull(options.BearerToken);
        Assert.IsNull(options.AwsSigV4);
        Assert.IsNull(options.ForwardProxy);
        Assert.IsFalse(options.ProxyTunnel);
        Assert.IsFalse(options.OverUnixSocket);
    }

    [TestMethod]
    public void HttpRequestOptions_EveryMemberSet_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] headers = ["X-One: 1", "Accept:"];
        string[] proxyHeaders = ["X-Proxy: 1"];
        var body = new BytesBody(new byte[] { 0x61 }, "application/x-www-form-urlencoded");
        var proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "proxy.example", 1080, new NetworkCredential("u", "p"));
        diagnostics.Arrange("headers", string.Join("|", headers));
        diagnostics.Arrange("proxy headers", string.Join("|", proxyHeaders));
        diagnostics.Arrange("body", body);
        diagnostics.Arrange("forward proxy host", proxy.Host);

        var options = new HttpRequestOptions
        {
            CustomMethod = "PATCH",
            Headers = headers,
            ProxyHeaders = proxyHeaders,
            UserAgent = "",
            Referer = "https://example.com/from",
            Body = body,
            FollowRedirects = true,
            MaxRedirects = -1,
            RedirectsFollowed = 7,
            Fail = HttpFailMode.FailWithBody,
            Version = HttpVersionPreference.Http10,
            HappyEyeballsTimeout = TimeSpan.FromMilliseconds(1000),
            ContinueWait = TimeSpan.FromMilliseconds(200),
            Compressed = true,
            TransferEncoding = true,
            Raw = true,
            IgnoreContentLength = true,
            RequestTarget = "*",
            AuthSchemes = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer,
            BearerToken = "token",
            AwsSigV4 = "aws:amz",
            ForwardProxy = proxy,
            ProxyTunnel = true,
            OverUnixSocket = true,
        };

        diagnostics.Act("custom method", options.CustomMethod);
        diagnostics.Act("max redirects", options.MaxRedirects);
        diagnostics.Act("fail mode", options.Fail);
        diagnostics.Act("version", options.Version);
        diagnostics.Act("auth schemes", options.AuthSchemes);
        diagnostics.Assert("custom method", "PATCH", options.CustomMethod);
        diagnostics.Assert("version", HttpVersionPreference.Http10, options.Version);
        diagnostics.Assert("auth schemes", HttpAuthSchemes.Any | HttpAuthSchemes.Bearer, options.AuthSchemes);
        Assert.AreEqual("PATCH", options.CustomMethod);
        Assert.AreSame(headers, options.Headers);
        Assert.AreSame(proxyHeaders, options.ProxyHeaders);
        Assert.AreEqual(string.Empty, options.UserAgent);
        Assert.AreEqual("https://example.com/from", options.Referer);
        Assert.AreSame(body, options.Body);
        Assert.IsTrue(options.FollowRedirects);
        Assert.AreEqual(-1, options.MaxRedirects);
        Assert.AreEqual(7, options.RedirectsFollowed);
        Assert.AreEqual(HttpFailMode.FailWithBody, options.Fail);
        Assert.AreEqual(HttpVersionPreference.Http10, options.Version);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000), options.HappyEyeballsTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), options.ContinueWait);
        Assert.IsTrue(options.Compressed);
        Assert.IsTrue(options.TransferEncoding);
        Assert.IsTrue(options.Raw);
        Assert.IsTrue(options.IgnoreContentLength);
        Assert.AreEqual("*", options.RequestTarget);
        Assert.AreEqual(HttpAuthSchemes.Any | HttpAuthSchemes.Bearer, options.AuthSchemes);
        Assert.AreEqual("token", options.BearerToken);
        Assert.AreEqual("aws:amz", options.AwsSigV4);
        Assert.AreSame(proxy, options.ForwardProxy);
        Assert.IsTrue(options.ProxyTunnel);
        Assert.IsTrue(options.OverUnixSocket);
    }

    [TestMethod]
    public void Equals_ForTwoDefaultInstances_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var first = new HttpRequestOptions();
        var second = new HttpRequestOptions();
        diagnostics.Arrange("instances", "two default HttpRequestOptions");

        bool equal = first.Equals(second);

        diagnostics.Act("equal", equal);
        diagnostics.Act("hash codes equal", first.GetHashCode() == second.GetHashCode());
        diagnostics.Assert("equal", true, equal);
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void TriesTcpBeforeQuic_ByDefault_IsFalseSoHttp3StartsWithQuic()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tcp first attempt version", null);

        bool triesTcpFirst = new HttpRequestOptions().TriesTcpBeforeQuic;

        diagnostics.Act("tries tcp before quic", triesTcpFirst);
        diagnostics.Assert("tries tcp before quic", false, triesTcpFirst);
        Assert.IsFalse(new HttpRequestOptions().TriesTcpBeforeQuic);
    }

    [TestMethod]
    public void TriesTcpBeforeQuic_WithATcpFirstAttemptVersion_IsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tcp first attempt version", "h1");

        bool triesTcpFirst = new HttpRequestOptions { TcpFirstAttemptVersion = "h1" }.TriesTcpBeforeQuic;

        diagnostics.Act("tries tcp before quic", triesTcpFirst);
        diagnostics.Assert("tries tcp before quic", true, triesTcpFirst);
        Assert.IsTrue(new HttpRequestOptions { TcpFirstAttemptVersion = "h1" }.TriesTcpBeforeQuic);
    }

    [TestMethod]
    public void With_ChangingOneMember_KeepsTheRest()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var get = new HttpRequestOptions { UserAgent = "agent/1", FollowRedirects = true };
        diagnostics.Arrange("user agent", get.UserAgent);
        diagnostics.Arrange("follow redirects", get.FollowRedirects);

        var post = get with { CustomMethod = "POST" };

        diagnostics.Act("custom method", post.CustomMethod);
        diagnostics.Act("user agent", post.UserAgent);
        diagnostics.Act("follow redirects", post.FollowRedirects);
        diagnostics.Assert("custom method", "POST", post.CustomMethod);
        diagnostics.Assert("user agent", "agent/1", post.UserAgent);
        Assert.AreEqual("POST", post.CustomMethod);
        Assert.AreEqual("agent/1", post.UserAgent);
        Assert.IsTrue(post.FollowRedirects);
        Assert.AreNotEqual(get, post);
    }
}
