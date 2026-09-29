using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="NameResolutionFailure" /> to curl's messages for a name that did not resolve:
/// the Schannel build's plain one, and the c-ares build's with the reason or exit 43 (measured on
/// curl 8.22.0 with c-ares 1.34.8, BL-643 and BL-694).
/// </summary>
[TestClass]
public sealed class NameResolutionFailureTests
{
    [TestMethod]
    public void Describe_WithoutAReason_IsThePlainMessage()
    {
        var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", "bl694.example", 1, DnsLookupFailure.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, exitCode);
        Assert.AreEqual("Could not resolve host: bl694.example", message);
    }

    [TestMethod]
    public void Describe_WithAReason_AddsItInBrackets()
    {
        var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", "bl694.example", 1, DnsLookupFailure.Timeout);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, exitCode);
        Assert.AreEqual("Could not resolve host: bl694.example (Timeout while contacting DNS servers)", message);
    }

    [TestMethod]
    public void Describe_AProxy_NamesTheProxy()
    {
        var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveProxy, "proxy", "proxy.example", 3128, DnsLookupFailure.NotFound);

        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, exitCode);
        Assert.AreEqual("Could not resolve proxy: proxy.example (Domain name not found)", message);
    }

    [TestMethod]
    public void Describe_ABadConfiguration_IsExit43WithTheHostAndPort()
    {
        var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", "bl643.example", 1, DnsLookupFailure.BadConfiguration);

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, exitCode);
        Assert.AreEqual("Error 43 resolving bl643.example:1", message);
    }

    [TestMethod]
    public void Describe_ALongHost_IsCutTo255Characters()
    {
        var (_, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", new string('a', 300), 1, DnsLookupFailure.NotFound);

        Assert.AreEqual(255, message.Length);
    }
}
