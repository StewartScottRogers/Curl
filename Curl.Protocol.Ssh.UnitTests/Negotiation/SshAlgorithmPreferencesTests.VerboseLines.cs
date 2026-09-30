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
        Assert.AreEqual("WinCNG", SshAlgorithmPreferences.WindowsReference.CryptographyBackend);
        Assert.AreEqual("OpenSSL", SshAlgorithmPreferences.OpenSslReference.CryptographyBackend);
        Assert.IsNull(SshAlgorithmPreferences.Full.CryptographyBackend);
    }

    [TestMethod]
    [DataRow("ssh-rsa", "rsa-sha2-256,rsa-sha2-512,ssh-rsa")]
    [DataRow("ssh-ed25519", "ssh-ed25519")]
    public void NarrowedHostKeyNames_AreTheNamesCurlSets(string keyType, string expected) =>
        Assert.AreEqual(expected, SshAlgorithmPreferences.NarrowedHostKeyNames(keyType));
}
