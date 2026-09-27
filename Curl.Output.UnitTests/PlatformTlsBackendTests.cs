namespace Curl.Output;

/// <summary>
/// Pins <see cref="PlatformTlsBackend"/> to ADR-0009: Schannel's wording on Windows, OpenSSL's elsewhere.
/// </summary>
[TestClass]
public sealed class PlatformTlsBackendTests
{
    [TestMethod]
    [DataRow(true, TlsBackend.Schannel)]
    [DataRow(false, TlsBackend.OpenSsl)]
    public void ForPlatform_ChoosesThePlatformsCurlBuild(bool isWindows, TlsBackend expected)
    {
        Assert.AreEqual(expected, PlatformTlsBackend.ForPlatform(isWindows));
    }

    [TestMethod]
    public void ForProcess_IsTheRunningPlatformsBackend()
    {
        Assert.AreEqual(PlatformTlsBackend.ForPlatform(OperatingSystem.IsWindows()), PlatformTlsBackend.ForProcess);
    }
}
