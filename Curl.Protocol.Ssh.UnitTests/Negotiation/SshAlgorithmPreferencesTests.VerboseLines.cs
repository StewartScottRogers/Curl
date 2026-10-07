using Curl.Testing;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// Pins what <see cref="SshAlgorithmPreferences" /> gives curl 8.21.0's <c>-v</c> lines: the
/// platform build's cryptography backend and the host-key names a known-hosts entry
/// narrows to (BL-578, ADR-0262).
/// </summary>
public sealed partial class SshAlgorithmPreferencesTests
{
    [TestMethod]
    public void CryptographyBackend_IsThePlatformBuildsBackend()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("presets", "WindowsReference, OpenSslReference, Full");

        string? windows = SshAlgorithmPreferences.WindowsReference.CryptographyBackend;
        string? openSsl = SshAlgorithmPreferences.OpenSslReference.CryptographyBackend;
        string? full = SshAlgorithmPreferences.Full.CryptographyBackend;

        diagnostics.Act("backends", $"{windows ?? "(null)"}, {openSsl ?? "(null)"}, {full ?? "(null)"}");
        diagnostics.Assert("WindowsReference backend", "WinCNG", windows);
        diagnostics.Assert("OpenSslReference backend", "OpenSSL", openSsl);
        diagnostics.Assert("Full backend", "(null)", full ?? "(null)");
        Assert.AreEqual("WinCNG", windows);
        Assert.AreEqual("OpenSSL", openSsl);
        Assert.IsNull(full);
    }

    [TestMethod]
    [DataRow("ssh-rsa", "rsa-sha2-256,rsa-sha2-512,ssh-rsa")]
    [DataRow("ssh-ed25519", "ssh-ed25519")]
    public void NarrowedHostKeyNames_AreTheNamesCurlSets(string keyType, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("known host key type", keyType);

        string actual = SshAlgorithmPreferences.NarrowedHostKeyNames(keyType);

        diagnostics.Act("narrowed names", actual);
        diagnostics.Diff("narrowed names", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
