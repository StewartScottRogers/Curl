using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// Pins the presets to the name-lists decoded from each reference build's <c>KEXINIT</c>
/// (ADR-0122, measured 2026-09-28), written out here independently of the production
/// strings.
/// </summary>
[TestClass]
public sealed partial class SshAlgorithmPreferencesTests
{
    [TestMethod]
    public void WindowsReference_IsCurl8210WinCngKexInit()
    {
        AssertLists(
            SshAlgorithmPreferences.WindowsReference,
            "diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com",
            "rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com",
            "chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc",
            "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96",
            "none");
    }

    [TestMethod]
    public void OpenSslReference_IsCurl8210OpenSslKexInit()
    {
        AssertLists(
            SshAlgorithmPreferences.OpenSslReference,
            "curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com",
            "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com",
            "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc",
            "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96,hmac-ripemd160,hmac-ripemd160@openssh.com",
            "none");
    }

    [TestMethod]
    public void Full_IsAdr0122sFullSetOrder()
    {
        AssertLists(
            SshAlgorithmPreferences.Full,
            "mlkem768x25519-sha256,mlkem768nistp256-sha256,mlkem1024nistp384-sha384,sntrup761x25519-sha512,sntrup761x25519-sha512@openssh.com,curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com",
            "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,ecdsa-sha2-nistp384-cert-v01@openssh.com,sk-ssh-ed25519-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,sk-ssh-ed25519@openssh.com,sk-ecdsa-sha2-nistp256@openssh.com,rsa-sha2-512,rsa-sha2-256,sk-ecdsa-sha2-nistp256-cert-v01@openssh.com,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com,ssh-dss",
            "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc",
            "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96,hmac-ripemd160,hmac-ripemd160@openssh.com,hmac-md5-etm@openssh.com",
            "none");
    }

    [TestMethod]
    [DataRow("Windows", "rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "ecdsa-sha2-nistp256-cert-v01@openssh.com,ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa-cert-v01@openssh.com")]
    [DataRow("Full", "")]
    public void HostKeysNeverAgreed_IsEveryOfferedCertificateButEd25519sOnTheReferenceBuilds_AsMeasured(string preset, string expected)
    {
        SshAlgorithmPreferences preferences = preset switch
        {
            "Windows" => SshAlgorithmPreferences.WindowsReference,
            "OpenSSL" => SshAlgorithmPreferences.OpenSslReference,
            _ => SshAlgorithmPreferences.Full,
        };

        Assert.AreEqual(expected, string.Join(',', preferences.HostKeysNeverAgreed));
    }

    [TestMethod]
    public void WithCompression_True_OffersZlibThenZlibOpenSshThenNone_AsMeasured()
    {
        SshAlgorithmPreferences compressed = SshAlgorithmPreferences.WindowsReference.WithCompression(true);

        Assert.AreEqual("zlib,zlib@openssh.com,none", string.Join(',', compressed.Compression));
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference.KeyExchange, compressed.KeyExchange);
    }

    [TestMethod]
    public void WithCompression_False_ChangesNothing()
    {
        Assert.AreSame(SshAlgorithmPreferences.OpenSslReference, SshAlgorithmPreferences.OpenSslReference.WithCompression(false));
    }

    [TestMethod]
    public void NarrowHostKeysTo_SshRsaOnWindows_IsRsaSha2256ThenRsaSha2512ThenSshRsa_AsMeasured()
    {
        SshAlgorithmPreferences narrowed = SshAlgorithmPreferences.WindowsReference.NarrowHostKeysTo("ssh-rsa");

        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    [DataRow("ssh-ed25519")]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    public void NarrowHostKeysTo_TypeTheOpenSslBuildHolds_IsThatTypeAlone(string keyType)
    {
        SshAlgorithmPreferences narrowed = SshAlgorithmPreferences.OpenSslReference.NarrowHostKeysTo(keyType);

        Assert.AreEqual(keyType, string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    public void NarrowHostKeysTo_SshDssInTheFullSet_IsSshDssAlone()
    {
        Assert.AreEqual("ssh-dss", string.Join(',', SshAlgorithmPreferences.Full.NarrowHostKeysTo("ssh-dss").ServerHostKey));
    }

    [TestMethod]
    [DataRow("ssh-ed25519", DisplayName = "measured on the WinCNG build")]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ssh-dss")]
    public void NarrowHostKeysTo_TypeTheWindowsBuildLacks_FailsWithExit79(string keyType)
    {
        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(
            () => SshAlgorithmPreferences.WindowsReference.NarrowHostKeysTo(keyType));

        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual($"libssh2 method '{keyType}' failed: The requested method(s) are not currently supported", failure.Message);
    }

    [TestMethod]
    public void NarrowHostKeysTo_UnknownType_ChangesNothing()
    {
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, SshAlgorithmPreferences.WindowsReference.NarrowHostKeysTo("ssh-foo"));
    }

    private static void AssertLists(
        SshAlgorithmPreferences preferences,
        string keyExchange,
        string hostKey,
        string cipher,
        string mac,
        string compression)
    {
        Assert.AreEqual(keyExchange, string.Join(',', preferences.KeyExchange));
        Assert.AreEqual(hostKey, string.Join(',', preferences.ServerHostKey));
        Assert.AreEqual(cipher, string.Join(',', preferences.Cipher));
        Assert.AreEqual(mac, string.Join(',', preferences.Mac));
        Assert.AreEqual(compression, string.Join(',', preferences.Compression));
    }
}
