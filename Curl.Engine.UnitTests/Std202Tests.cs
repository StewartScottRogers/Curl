using System.Net;
using System.Text;
using Curl.Api.Options.Proxy;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins STD-202 (BL-5883): the <c>-U</c> user and password are percent-decoded before they reach the
/// proxy, as curl 8.22.0 decodes them (measured on Windows: <c>-U u%3Ax:p%40ss</c> sends
/// <c>Basic dTp4OnBAc3M=</c>, <c>%zz</c> stays as it is, and <c>%00</c> is exit 3 with or without a proxy).
/// </summary>
[TestClass]
public sealed class Std202Tests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("u%3Ax", "p%40ss", "u:x", "p@ss")]
    [DataRow("us%20er", "p%40ss", "us er", "p@ss")]
    [DataRow("u%zz%4", "%", "u%zz%4", "%")]
    [DataRow("", "", "", "")]
    public void TrySelect_PercentEncodedProxyUser_GivesTheProxyTheDecodedCredential(string user, string password, string expectedUser, string expectedPassword)
    {
        EasyProxyOptions options = new() { Proxy = "http://127.0.0.1:32090" };
        Diagnostics.Arrange("-U", $"{user}:{password}");

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential(user, password), Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("credential", $"{proxy?.Credential?.UserName}:{proxy?.Credential?.Password}");

        Assert.AreEqual(expectedUser, proxy?.Credential?.UserName);
        Assert.AreEqual(expectedPassword, proxy?.Credential?.Password);
    }

    [TestMethod]
    public void TrySelect_ProxyUserWithEncodedBytesOutsideAscii_KeepsThoseBytes()
    {
        EasyProxyOptions options = new() { Proxy = "socks5://127.0.0.1:32090" };
        Encoding sent = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential("%ff%C3%A9", "%e9"), Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("user bytes", Convert.ToHexString(sent.GetBytes(proxy!.Credential!.UserName)));

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xC3, 0xA9 }, sent.GetBytes(proxy.Credential.UserName));
        CollectionAssert.AreEqual(new byte[] { 0xE9 }, sent.GetBytes(proxy.Credential.Password));
    }

    [TestMethod]
    public void TrySelect_PercentEncodedProxyUserWithOnlyAPreProxy_GivesTheDecodedCredential()
    {
        EasyProxyOptions options = new() { PreProxy = "socks5://127.0.0.1:32091" };

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential("u%3Ax", "p%40ss"), Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("credential", $"{proxy?.Credential?.UserName}:{proxy?.Credential?.Password}");

        Assert.AreEqual("u:x", proxy?.Credential?.UserName);
        Assert.AreEqual("p@ss", proxy?.Credential?.Password);
    }

    [TestMethod]
    [DataRow("u%00x", "p", "http://example.test/", "http://127.0.0.1:32090")]
    [DataRow("u", "p%00", "http://example.test/", "http://a:b@127.0.0.1:32090")]
    [DataRow("u%00", "p", "http://example.test/", null)]
    [DataRow("u%00", "p", "file:///dir/x", null)]
    public void TrySelect_ProxyUserDecodingToNul_FailsWithUrlMalformat(string user, string password, string url, string? proxyText)
    {
        EasyProxyOptions options = new() { Proxy = proxyText };
        Diagnostics.Arrange("proxy / URL", $"{proxyText} / {url}");

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential(user, password), CurlUrl.Parse(url), out ProxyEndpoint? proxy, out TransferResult? failure);
        Diagnostics.Act("failure", failure?.ErrorMessage);

        Assert.IsFalse(selected);
        Assert.IsNull(proxy);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure?.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", failure?.ErrorMessage);
    }
}
