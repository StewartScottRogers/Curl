using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="NameResolutionFailure" /> to curl's messages for a name that did not resolve:
/// the Schannel build's plain one, and the c-ares build's with the reason or exit 43 (measured on
/// curl 8.22.0 with c-ares 1.34.8, BL-643 and BL-694).
/// </summary>
[TestClass]
public sealed class NameResolutionFailureTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Describe_WithoutAReason_IsThePlainMessage()
    {
        var (exitCode, message) = Describe(CurlExitCode.CouldntResolveHost, "host", "bl694.example", 1, DnsLookupFailure.None);

        AssertDescription(CurlExitCode.CouldntResolveHost, "Could not resolve host: bl694.example", exitCode, message);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, exitCode);
        Assert.AreEqual("Could not resolve host: bl694.example", message);
    }

    [TestMethod]
    public void Describe_WithAReason_AddsItInBrackets()
    {
        var (exitCode, message) = Describe(CurlExitCode.CouldntResolveHost, "host", "bl694.example", 1, DnsLookupFailure.Timeout);

        AssertDescription(CurlExitCode.CouldntResolveHost, "Could not resolve host: bl694.example (Timeout while contacting DNS servers)", exitCode, message);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, exitCode);
        Assert.AreEqual("Could not resolve host: bl694.example (Timeout while contacting DNS servers)", message);
    }

    [TestMethod]
    public void Describe_AProxy_NamesTheProxy()
    {
        var (exitCode, message) = Describe(CurlExitCode.CouldntResolveProxy, "proxy", "proxy.example", 3128, DnsLookupFailure.NotFound);

        AssertDescription(CurlExitCode.CouldntResolveProxy, "Could not resolve proxy: proxy.example (Domain name not found)", exitCode, message);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, exitCode);
        Assert.AreEqual("Could not resolve proxy: proxy.example (Domain name not found)", message);
    }

    [TestMethod]
    public void Describe_ABadConfiguration_IsExit43WithTheHostAndPort()
    {
        var (exitCode, message) = Describe(CurlExitCode.CouldntResolveHost, "host", "bl643.example", 1, DnsLookupFailure.BadConfiguration);

        AssertDescription(CurlExitCode.BadFunctionArgument, "Error 43 resolving bl643.example:1", exitCode, message);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, exitCode);
        Assert.AreEqual("Error 43 resolving bl643.example:1", message);
    }

    [TestMethod]
    public void Describe_ALongHost_IsCutTo255Characters()
    {
        var (_, message) = Describe(CurlExitCode.CouldntResolveHost, "host", new string('a', 300), 1, DnsLookupFailure.NotFound);

        Diagnostics.Assert("message length", 255, message.Length);
        Assert.AreEqual(255, message.Length);
    }

    private (CurlExitCode ExitCode, string Message) Describe(CurlExitCode exitCode, string kind, string host, int port, DnsLookupFailure failure)
    {
        Diagnostics.Arrange("exit code", exitCode);
        Diagnostics.Arrange("kind", kind);
        Diagnostics.Arrange("host", host.Length > 64 ? $"{host[..64]}... ({host.Length} characters)" : host);
        Diagnostics.Arrange("port", port);
        Diagnostics.Arrange("failure", failure);

        var described = NameResolutionFailure.Describe(exitCode, kind, host, port, failure);

        Diagnostics.Act("exit code", described.ExitCode);
        Diagnostics.Act("message", described.Message);
        return described;
    }

    private void AssertDescription(CurlExitCode expectedExitCode, string expectedMessage, CurlExitCode exitCode, string message)
    {
        Diagnostics.Assert("exit code", expectedExitCode, exitCode);
        Diagnostics.Diff("message", expectedMessage, message);
    }
}
