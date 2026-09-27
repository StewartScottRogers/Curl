using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="ProxyUrlParser" /> reads proxy text, each case measured against
/// curl 8.21.0 on 2026-09-26 as <c>curl -sSv --connect-timeout 1 -x "&lt;text&gt;" http://a.test:2222/</c>,
/// reading the address in <c>Trying</c> or the <c>curl: (N)</c> line; credentials were read
/// from the <c>Proxy-Authorization</c> header a loopback listener on port 1111 received.
/// </summary>
[TestClass]
public sealed class ProxyUrlParserTests
{
    [TestMethod]
    [DataRow("127.0.0.1", ProxyKind.Http, "127.0.0.1", 80)]
    [DataRow("http://127.0.0.1", ProxyKind.Http, "127.0.0.1", 80)]
    [DataRow("https://127.0.0.1", ProxyKind.Https, "127.0.0.1", 443)]
    [DataRow("socks://127.0.0.1", ProxyKind.Socks4, "127.0.0.1", 1080)]
    [DataRow("SOCKS://127.0.0.1:1111", ProxyKind.Socks4, "127.0.0.1", 1111)]
    [DataRow("socks4://127.0.0.1", ProxyKind.Socks4, "127.0.0.1", 1080)]
    [DataRow("socks4a://127.0.0.1", ProxyKind.Socks4a, "127.0.0.1", 1080)]
    [DataRow("socks5://127.0.0.1", ProxyKind.Socks5, "127.0.0.1", 1080)]
    [DataRow("socks5h://127.0.0.1", ProxyKind.Socks5Hostname, "127.0.0.1", 1080)]
    [DataRow("HTTP://127.0.0.1:1111", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("SOCKS5H://127.0.0.1:1111", ProxyKind.Socks5Hostname, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:1111/some/path", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:1111?q", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:1111#f", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:1111/", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:001111", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http://127.0.0.1:", ProxyKind.Http, "127.0.0.1", 80)]
    [DataRow("http:/127.0.0.1:1111", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("http:///127.0.0.1:1111", ProxyKind.Http, "127.0.0.1", 1111)]
    [DataRow("socks5:/127.0.0.1:1111", ProxyKind.Socks5, "127.0.0.1", 1111)]
    [DataRow("[::1]:1111", ProxyKind.Http, "::1", 1111)]
    [DataRow("[::1]", ProxyKind.Http, "::1", 80)]
    [DataRow("http://[::1]:", ProxyKind.Http, "::1", 80)]
    [DataRow("http://[::1%25eth0]:1111", ProxyKind.Http, "::1", 1111)]
    [DataRow("http://a_b:1111", ProxyKind.Http, "a_b", 1111)]
    [DataRow("http://a~b:1111", ProxyKind.Http, "a~b", 1111)]
    [DataRow("http://a%7eb:1111", ProxyKind.Http, "a~b", 1111)]
    [DataRow("http://ex%41mple:1111", ProxyKind.Http, "exAmple", 1111)]
    [DataRow("http://@127.0.0.1:1111", ProxyKind.Http, "127.0.0.1", 1111)]
    public void TryParse_MeasuredProxy_GivesKindHostAndPort(string text, ProxyKind kind, string host, int port)
    {
        Assert.IsTrue(ProxyUrlParser.TryParse(text, out ProxyEndpoint? proxy, out TransferResult? failure));
        Assert.IsNull(failure);
        Assert.AreEqual(new ProxyEndpoint(kind, host, port, null), proxy);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http, 80)]
    [DataRow(ProxyKind.Http10, 80)]
    [DataRow(ProxyKind.Https, 443)]
    [DataRow(ProxyKind.Socks4, 1080)]
    [DataRow(ProxyKind.Socks4a, 1080)]
    [DataRow(ProxyKind.Socks5, 1080)]
    [DataRow(ProxyKind.Socks5Hostname, 1080)]
    public void TryParse_NoSchemeWithAnOptionKind_IsThatKindOnItsDefaultPort(ProxyKind kind, int port)
    {
        Assert.IsTrue(ProxyUrlParser.TryParse("127.0.0.1", kind, out ProxyEndpoint? proxy, out TransferResult? failure));
        Assert.IsNull(failure);
        Assert.AreEqual(new ProxyEndpoint(kind, "127.0.0.1", port, null), proxy);
    }

    [TestMethod]
    public void TryParse_SchemeWithAnOptionKind_IsTheSchemesKind()
    {
        // Measured: --socks5 http://127.0.0.1:P speaks HTTP to the proxy.
        Assert.IsTrue(ProxyUrlParser.TryParse("http://127.0.0.1:1111", ProxyKind.Socks5, out ProxyEndpoint? proxy, out _));
        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 1111, null), proxy);
    }

    [TestMethod]
    public void TryParse_NoSchemeWithAnOptionKindAndAPort_KeepsThePort()
    {
        Assert.IsTrue(ProxyUrlParser.TryParse("127.0.0.1:1111", ProxyKind.Socks4, out ProxyEndpoint? proxy, out _));
        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Socks4, "127.0.0.1", 1111, null), proxy);
    }

    [TestMethod]
    [DataRow("http://%41:%42@127.0.0.1:1111", "A", "B")]
    [DataRow("http://u@127.0.0.1:1111", "u", "")]
    [DataRow("http://u:@127.0.0.1:1111", "u", "")]
    [DataRow("http://%zz:p@127.0.0.1:1111", "%zz", "p")]
    [DataRow("http://u:p@[::1]:1111", "u", "p")]
    public void TryParse_UserInformation_IsTheDecodedCredential(string text, string user, string password)
    {
        Assert.IsTrue(ProxyUrlParser.TryParse(text, out ProxyEndpoint? proxy, out _));
        Assert.AreEqual(user, proxy.Credential!.UserName);
        Assert.AreEqual(password, proxy.Credential.Password);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:99999", ProxyUrlParser.BadPortReason)]
    [DataRow("http://127.0.0.1:65536", ProxyUrlParser.BadPortReason)]
    [DataRow("http://127.0.0.1:abc", ProxyUrlParser.BadPortReason)]
    [DataRow("http://127.0.0.1:-1", ProxyUrlParser.BadPortReason)]
    [DataRow("http://127.0.0.1:+1111", ProxyUrlParser.BadPortReason)]
    [DataRow("http://127.0.0.1:1111:2", ProxyUrlParser.BadPortReason)]
    [DataRow("http://1:2:3", ProxyUrlParser.BadPortReason)]
    [DataRow("https://127.0.0.1:abc", ProxyUrlParser.BadPortReason)]
    [DataRow("socks5://127.0.0.1:99999", ProxyUrlParser.BadPortReason)]
    [DataRow("localhost:abc", ProxyUrlParser.BadPortReason)]
    [DataRow("127.0.0.1:", ProxyUrlParser.BadPortReason)]
    [DataRow("[::1]:", ProxyUrlParser.BadPortReason)]
    [DataRow("http:127.0.0.1:1111", ProxyUrlParser.BadPortReason)]
    [DataRow("http:127.0.0.1", ProxyUrlParser.BadPortReason)]
    [DataRow("http:", ProxyUrlParser.BadPortReason)]
    [DataRow("foo:x", ProxyUrlParser.BadPortReason)]
    [DataRow("foo://a:abc", ProxyUrlParser.BadPortReason)]
    [DataRow("http://[::1]x", ProxyUrlParser.BadPortReason)]
    [DataRow("http://:abc", ProxyUrlParser.BadPortReason)]
    [DataRow("http://", ProxyUrlParser.NoHostReason)]
    [DataRow(":1111", ProxyUrlParser.NoHostReason)]
    [DataRow("http://u:p@:1", ProxyUrlParser.NoHostReason)]
    [DataRow("foo://", ProxyUrlParser.NoHostReason)]
    [DataRow("http://[::1", ProxyUrlParser.BadIPv6Reason)]
    [DataRow("http://[zz]:1111", ProxyUrlParser.BadIPv6Reason)]
    [DataRow("http://a b:1", ProxyUrlParser.MalformedReason)]
    [DataRow("http://127.0.0.1: 1111", ProxyUrlParser.MalformedReason)]
    [DataRow(" http://127.0.0.1:1111", ProxyUrlParser.MalformedReason)]
    [DataRow("http://a\tb:1", ProxyUrlParser.MalformedReason)]
    [DataRow("http://ab:1\u007f", ProxyUrlParser.MalformedReason)]
    [DataRow("http:////127.0.0.1:1111", ProxyUrlParser.SlashesReason)]
    [DataRow("foo:////x", ProxyUrlParser.SlashesReason)]
    [DataRow("a@b@127.0.0.1:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a<b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a*b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a%20b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a!b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a,b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a+b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a=b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a;b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a%zzb:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a%2fb:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a$b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a|b:1111", ProxyUrlParser.BadHostnameReason)]
    [DataRow("http://a{b:1111", ProxyUrlParser.BadHostnameReason)]
    public void TryParse_MeasuredSyntaxError_IsExit5WithCurlsReason(string text, string reason)
    {
        Assert.IsFalse(ProxyUrlParser.TryParse(text, out ProxyEndpoint? proxy, out TransferResult? failure));
        Assert.IsNull(proxy);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, failure.ExitCode);
        Assert.AreEqual($"Unsupported proxy syntax in '{text}': {reason}", failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("foo://127.0.0.1:1111")]
    [DataRow("FOO://x")]
    [DataRow("foo:/x")]
    public void TryParse_UnknownScheme_IsExit7(string text)
    {
        Assert.IsFalse(ProxyUrlParser.TryParse(text, out _, out TransferResult? failure));
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual($"Unsupported proxy scheme for '{text}'", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_PortZero_IsTheMeasuredConnectFailure()
    {
        Assert.IsFalse(ProxyUrlParser.TryParse("http://localhost:0", out _, out TransferResult? failure));
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual(
            "Failed to connect to localhost:0 over proxy localhost after 0 ms: Could not connect to server",
            failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProxyUrlParser.TryParse(null!, out _, out _));
    }
}
