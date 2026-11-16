using Curl.Ssh.Fakes;
using Curl.Ssh.PacketProtection;

namespace Curl.Ssh.Negotiation;

/// <summary>
/// Pins that the OpenSSL preset offers libssh2 1.11.1's OpenSSL lists in its order, and that
/// every weak algorithm it offers (Blowfish, CAST-128, RIPEMD-160) is one the packet layer
/// implements, so a server offering only one of them agrees it (BL-5283, ADR-1278).
/// </summary>
[TestClass]
public sealed class SshAlgorithmPreferencesOpenSslTests
{
    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["zlib", "zlib@openssh.com", "none"]));

    [TestMethod]
    public void ForClient_OpenSslReference_ListsTheKeyExchangesStartingWithCurve25519ThenEcdh()
    {
        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0));

        CollectionAssert.AreEqual(
            new[] { "curve25519-sha256", "curve25519-sha256@libssh.org", "ecdh-sha2-nistp256", "ecdh-sha2-nistp384", "ecdh-sha2-nistp521", "diffie-hellman-group-exchange-sha256" },
            kexInit.KeyExchange.Take(6).ToArray());
    }

    [TestMethod]
    public void ForClient_OpenSslReference_ListsEcdsaAndEd25519HostKeysBeforeRsa()
    {
        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0));

        string[] hostKeys = kexInit.ServerHostKey.ToArray();

        Assert.AreEqual("ecdsa-sha2-nistp256", hostKeys[0]);
        Assert.IsTrue(Array.IndexOf(hostKeys, "ssh-ed25519") < Array.IndexOf(hostKeys, "rsa-sha2-512"));
    }

    [TestMethod]
    [DataRow("aes256-gcm@openssh.com")]
    [DataRow("blowfish-cbc")]
    [DataRow("cast128-cbc")]
    [DataRow("hmac-ripemd160")]
    [DataRow("hmac-ripemd160@openssh.com")]
    public void OpenSslReference_OffersTheAlgorithmAndThePacketLayerImplementsIt(string name)
    {
        SshAlgorithmPreferences preset = SshAlgorithmPreferences.OpenSslReference;

        Assert.IsTrue(preset.Cipher.Concat(preset.Mac).Contains(name));
        Assert.IsTrue(SshPacketProtections.Names.Contains(name) || name.StartsWith("aes256-gcm", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("blowfish-cbc")]
    [DataRow("cast128-cbc")]
    [DataRow("hmac-ripemd160")]
    [DataRow("hmac-ripemd160@openssh.com")]
    public void WindowsReference_DoesNotOfferTheWeakAlgorithmBecauseWinCngHasNone(string name)
    {
        SshAlgorithmPreferences preset = SshAlgorithmPreferences.WindowsReference;

        Assert.IsFalse(preset.Cipher.Concat(preset.Mac).Contains(name));
    }
}
