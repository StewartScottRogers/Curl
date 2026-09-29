using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins the host-key decision and its failures to curl 8.21.0's Windows build, measured
/// 2026-09-29 (BL-566) against a loopback SSH server: the messages below are curl's,
/// with the fingerprints of this test's fixed P-256 host key in place of the measured
/// key's.
/// </summary>
[TestClass]
public sealed class SshHostKeyCheckerTests
{
    private const string Md5 = "f844dafd8e0d77290b7d4e8377418b88";

    private const string Sha256 = "c4nEydciX4yHnXnQAkUIFU6HFGMS8bFT8zZXUIpUX+U=";

    private const string WrongMd5 = "00112233445566778899aabbccddeeff";

    private const string WrongSha256 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private const string Host = "127.0.0.1";

    private const int Port = 2222;

    private const string PeerFailedVerification = "SSL peer certificate or SSH remote key was not OK";

    private static readonly KnownHostsFile Matching = KnownHostsFile.Parse($"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n");

    private static readonly KnownHostsFile Mismatching = KnownHostsFile.Parse($"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {KnownHostsFileTests.OtherKeyBase64}\n");

    private static readonly KnownHostsFile Absent = KnownHostsFile.Parse($"[127.0.0.2]:2222 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n");

    [TestMethod]
    public void Check_KnownHostsMatch_Accepts() =>
        Check(new SshOptions(), Matching);

    [TestMethod]
    public void Check_KnownHostsAbsent_IsExit60()
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(new SshOptions(), Absent));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(PeerFailedVerification, exception.Message);
    }

    [TestMethod]
    public void Check_KnownHostsMismatch_IsExit60()
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(new SshOptions(), Mismatching));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(PeerFailedVerification, exception.Message);
    }

    [TestMethod]
    public void Check_Insecure_AcceptsAnyKey() =>
        Check(new SshOptions(), knownHosts: null);

    [TestMethod]
    [DataRow(Md5, DisplayName = "measured: lower case")]
    [DataRow("F844DAFD8E0D77290B7D4E8377418B88", DisplayName = "measured: upper case")]
    public void Check_Md5Matches_AcceptsWithoutKnownHosts(string md5) =>
        Check(new SshOptions { HostPublicKeyMd5 = md5 }, Absent);

    [TestMethod]
    [DataRow(Sha256, DisplayName = "measured: padded")]
    [DataRow("c4nEydciX4yHnXnQAkUIFU6HFGMS8bFT8zZXUIpUX+U", DisplayName = "measured: unpadded")]
    public void Check_Sha256Matches_AcceptsWithoutKnownHosts(string sha256) =>
        Check(new SshOptions { HostPublicKeySha256 = sha256 }, Absent);

    [TestMethod]
    [DataRow(true, DisplayName = "measured: with a matching known-hosts entry")]
    [DataRow(false, DisplayName = "measured: with -k")]
    public void Check_Md5Wrong_IsExit60WhateverKnownHostsSays(bool withKnownHosts) =>
        AssertDenied(
            new SshOptions { HostPublicKeyMd5 = WrongMd5 },
            withKnownHosts ? Matching : null,
            $"Denied establishing ssh session: mismatch MD5 fingerprint. Remote {Md5} is not equal to {WrongMd5}");

    [TestMethod]
    [DataRow(true, DisplayName = "measured: with a matching known-hosts entry")]
    [DataRow(false, DisplayName = "measured: with -k")]
    public void Check_Sha256Wrong_IsExit60WhateverKnownHostsSays(bool withKnownHosts) =>
        AssertDenied(
            new SshOptions { HostPublicKeySha256 = WrongSha256 },
            withKnownHosts ? Matching : null,
            $"Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {Sha256} is not equal to {WrongSha256}");

    [TestMethod]
    public void Check_Sha256RightMd5Wrong_ReportsTheMd5() =>
        AssertDenied(
            new SshOptions { HostPublicKeySha256 = Sha256, HostPublicKeyMd5 = WrongMd5 },
            Matching,
            $"Denied establishing ssh session: mismatch MD5 fingerprint. Remote {Md5} is not equal to {WrongMd5}");

    [TestMethod]
    public void Check_Sha256WrongMd5Right_ReportsTheSha256First() =>
        AssertDenied(
            new SshOptions { HostPublicKeySha256 = WrongSha256, HostPublicKeyMd5 = Md5 },
            Matching,
            $"Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {Sha256} is not equal to {WrongSha256}");

    [TestMethod]
    public void Check_Sha256WithTextAfterItsPadding_Accepts() =>
        Check(new SshOptions { HostPublicKeySha256 = Sha256 + "extra" }, Absent);

    [TestMethod]
    public void Check_Sha256ThatIsAPrefixOfTheFingerprint_IsRefused() =>
        AssertDenied(
            new SshOptions { HostPublicKeySha256 = "c4nEydciX4yHnX" },
            Matching,
            $"Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {Sha256} is not equal to c4nEydciX4yHnX");

    [TestMethod]
    public void NarrowHostKeys_AnRsaEntry_NarrowsAsMeasured()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"[127.0.0.1]:2222 ssh-rsa {KnownHostsFileTests.HostKeyBase64}");

        SshAlgorithmPreferences narrowed = Narrow(new SshOptions(), file);

        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    public void NarrowHostKeys_WithSha256_StillNarrows()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"127.0.0.1 ssh-rsa {KnownHostsFileTests.HostKeyBase64}");

        SshAlgorithmPreferences narrowed = Narrow(new SshOptions { HostPublicKeySha256 = Sha256 }, file);

        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    public void NarrowHostKeys_WithMd5_LeavesTheList() =>
        Assert.AreSame(
            SshAlgorithmPreferences.WindowsReference,
            Narrow(new SshOptions { HostPublicKeyMd5 = Md5 }, KnownHostsFile.Parse($"127.0.0.1 ssh-ed25519 {KnownHostsFileTests.HostKeyBase64}")));

    [TestMethod]
    public void NarrowHostKeys_Insecure_LeavesTheList() =>
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, Narrow(new SshOptions(), knownHosts: null));

    [TestMethod]
    public void NarrowHostKeys_NoEntryForTheHost_LeavesTheList() =>
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, Narrow(new SshOptions(), Absent));

    [TestMethod]
    public void NarrowHostKeys_AnRsa1Entry_IsExit79()
    {
        KnownHostsFile file = KnownHostsFile.Parse("[127.0.0.1]:2222 2048 65537 12345678901234567890123");

        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Narrow(new SshOptions(), file));

        Assert.AreEqual(CurlExitCode.Ssh, exception.ExitCode);
        Assert.AreEqual("Found host key type RSA1 which is not supported", exception.Message);
    }

    [TestMethod]
    [DataRow("ssh-dss", DisplayName = "measured: ssh-dss")]
    [DataRow("ssh-rsa-cert-v01@openssh.com", DisplayName = "measured: a certificate type")]
    [DataRow("foo-bar", DisplayName = "measured: an unknown name")]
    public void NarrowHostKeys_AnEntryOfAnUnknownType_IsExit79(string typeName)
    {
        KnownHostsFile file = KnownHostsFile.Parse($"[127.0.0.1]:2222 {typeName} {KnownHostsFileTests.HostKeyBase64}");

        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Narrow(new SshOptions(), file));

        Assert.AreEqual(CurlExitCode.Ssh, exception.ExitCode);
        Assert.AreEqual("Unknown host key type: 3932160", exception.Message);
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("ssh-ed25519")]
    public void NarrowHostKeys_EachOtherRecognizedType_NarrowsToItself(string typeName)
    {
        KnownHostsFile file = KnownHostsFile.Parse($"127.0.0.1 {typeName} {KnownHostsFileTests.HostKeyBase64}");

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.OpenSslReference, Host, Port, new SshOptions(), file);

        Assert.AreEqual(typeName, string.Join(',', narrowed.ServerHostKey));
    }

    private static void Check(SshOptions options, KnownHostsFile? knownHosts) =>
        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, knownHosts);

    private static SshAlgorithmPreferences Narrow(SshOptions options, KnownHostsFile? knownHosts) =>
        SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, options, knownHosts);

    private static void AssertDenied(SshOptions options, KnownHostsFile? knownHosts, string message)
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(options, knownHosts));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(message, exception.Message);
    }
}
