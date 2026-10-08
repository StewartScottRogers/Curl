using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="PlatformTlsBackend"/> to ADR-0009: Schannel's wording on Windows, OpenSSL's elsewhere.
/// </summary>
[TestClass]
public sealed class PlatformTlsBackendTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(true, TlsBackend.Schannel)]
    [DataRow(false, TlsBackend.OpenSsl)]
    public void ForPlatform_ChoosesThePlatformsCurlBuild(bool isWindows, TlsBackend expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", isWindows);

        TlsBackend actual = PlatformTlsBackend.ForPlatform(isWindows);

        diagnostics.Act("backend", actual);
        diagnostics.Assert("backend", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ForProcess_IsTheRunningPlatformsBackend()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TlsBackend expected = PlatformTlsBackend.ForPlatform(OperatingSystem.IsWindows());
        diagnostics.Arrange("expected backend for the running platform", expected);

        TlsBackend actual = PlatformTlsBackend.ForProcess;

        diagnostics.Act("ForProcess", actual);
        diagnostics.Assert("backend", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
