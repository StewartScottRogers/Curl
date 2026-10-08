using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Testing;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins the host-key decision and its failures to curl 8.21.0's Windows build, measured
/// 2026-09-29 (BL-566) against a loopback SSH server: the messages below are curl's,
/// with the fingerprints of this test's fixed P-256 host key in place of the measured
/// key's.
/// </summary>
[TestClass]
public sealed partial class SshHostKeyCheckerTests
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Check_KnownHostsMatch_Accepts() =>
        AssertAccepted(new SshOptions(), Matching);

    [TestMethod]
    public void Check_KnownHostsAbsent_IsExit60()
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(new SshOptions(), Absent));

        WriteFailure(CurlExitCode.PeerFailedVerification, PeerFailedVerification, exception);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(PeerFailedVerification, exception.Message);
    }

    [TestMethod]
    public void Check_KnownHostsMismatch_IsExit60()
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(new SshOptions(), Mismatching));

        WriteFailure(CurlExitCode.PeerFailedVerification, PeerFailedVerification, exception);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(PeerFailedVerification, exception.Message);
    }

    [TestMethod]
    public void Check_Insecure_AcceptsAnyKey() =>
        AssertAccepted(new SshOptions(), knownHosts: null);

    [TestMethod]
    [DataRow(Md5, DisplayName = "measured: lower case")]
    [DataRow("F844DAFD8E0D77290B7D4E8377418B88", DisplayName = "measured: upper case")]
    public void Check_Md5Matches_AcceptsWithoutKnownHosts(string md5) =>
        AssertAccepted(new SshOptions { HostPublicKeyMd5 = md5 }, Absent);

    [TestMethod]
    [DataRow(Sha256, DisplayName = "measured: padded")]
    [DataRow("c4nEydciX4yHnXnQAkUIFU6HFGMS8bFT8zZXUIpUX+U", DisplayName = "measured: unpadded")]
    public void Check_Sha256Matches_AcceptsWithoutKnownHosts(string sha256) =>
        AssertAccepted(new SshOptions { HostPublicKeySha256 = sha256 }, Absent);

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
        AssertAccepted(new SshOptions { HostPublicKeySha256 = Sha256 + "extra" }, Absent);

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

        AssertHostKeyList("rsa-sha2-256,rsa-sha2-512,ssh-rsa", narrowed);
        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    public void NarrowHostKeys_WithSha256_StillNarrows()
    {
        KnownHostsFile file = KnownHostsFile.Parse($"127.0.0.1 ssh-rsa {KnownHostsFileTests.HostKeyBase64}");

        SshAlgorithmPreferences narrowed = Narrow(new SshOptions { HostPublicKeySha256 = Sha256 }, file);

        AssertHostKeyList("rsa-sha2-256,rsa-sha2-512,ssh-rsa", narrowed);
        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
    }

    [TestMethod]
    public void NarrowHostKeys_WithMd5_LeavesTheList()
    {
        SshAlgorithmPreferences narrowed = Narrow(new SshOptions { HostPublicKeyMd5 = Md5 }, KnownHostsFile.Parse($"127.0.0.1 ssh-ed25519 {KnownHostsFileTests.HostKeyBase64}"));

        AssertUnchanged(narrowed);
        Assert.AreSame(
            SshAlgorithmPreferences.WindowsReference,
            narrowed);
    }

    [TestMethod]
    public void NarrowHostKeys_Insecure_LeavesTheList()
    {
        SshAlgorithmPreferences narrowed = Narrow(new SshOptions(), knownHosts: null);

        AssertUnchanged(narrowed);
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, narrowed);
    }

    [TestMethod]
    public void NarrowHostKeys_NoEntryForTheHost_LeavesTheList()
    {
        SshAlgorithmPreferences narrowed = Narrow(new SshOptions(), Absent);

        AssertUnchanged(narrowed);
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, narrowed);
    }

    [TestMethod]
    public void NarrowHostKeys_AnRsa1Entry_IsExit79()
    {
        KnownHostsFile file = KnownHostsFile.Parse("[127.0.0.1]:2222 2048 65537 12345678901234567890123");

        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Narrow(new SshOptions(), file));

        WriteFailure(CurlExitCode.Ssh, "Found host key type RSA1 which is not supported", exception);
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

        WriteFailure(CurlExitCode.Ssh, "Unknown host key type: 3932160", exception);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        KnownHostsFile file = KnownHostsFile.Parse($"127.0.0.1 {typeName} {KnownHostsFileTests.HostKeyBase64}");
        diagnostics.Arrange("preferences", "OpenSslReference");
        diagnostics.Arrange("known hosts", Entries(file));

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.OpenSslReference, Host, Port, new SshOptions(), file);

        AssertHostKeyList(typeName, narrowed);
        Assert.AreEqual(typeName, string.Join(',', narrowed.ServerHostKey));
    }

    private static string Entries(KnownHostsFile? file) =>
        file is null
            ? "(none, -k)"
            : $"{file.Entries.Count} entries [{string.Join(", ", file.Entries.Select(entry => $"{entry.PlainName ?? "(hashed)"} {entry.KeyType}"))}]";

    private static string Fingerprints(SshOptions options) =>
        $"MD5 {options.HostPublicKeyMd5 ?? "(none)"}, SHA256 {options.HostPublicKeySha256 ?? "(none)"}";

    private void WriteArrange(SshOptions options, KnownHostsFile? knownHosts)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("host and port", $"{Host}:{Port}");
        diagnostics.Arrange("fingerprints", Fingerprints(options));
        diagnostics.Arrange("known hosts", Entries(knownHosts));
    }

    private void Check(SshOptions options, KnownHostsFile? knownHosts)
    {
        WriteArrange(options, knownHosts);
        TestDiagnostics.For(TestContext).Bytes("host key", KnownHostsFileTests.HostKey);
        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, knownHosts);
    }

    private void AssertAccepted(SshOptions options, KnownHostsFile? knownHosts)
    {
        Check(options, knownHosts);

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Act("outcome", "accepted");
        diagnostics.Assert("outcome", "accepted", "accepted");
    }

    private SshAlgorithmPreferences Narrow(SshOptions options, KnownHostsFile? knownHosts)
    {
        WriteArrange(options, knownHosts);
        TestDiagnostics.For(TestContext).Arrange("preferences", "WindowsReference");
        return SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, options, knownHosts);
    }

    private void AssertHostKeyList(string expected, SshAlgorithmPreferences narrowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string actual = string.Join(',', narrowed.ServerHostKey);
        diagnostics.Act("server host key algorithms", actual);
        diagnostics.Diff("server host key algorithms", expected, actual);
    }

    private void AssertUnchanged(SshAlgorithmPreferences narrowed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Act("server host key algorithms", string.Join(',', narrowed.ServerHostKey));
        diagnostics.Assert("same preferences as WindowsReference", true, ReferenceEquals(SshAlgorithmPreferences.WindowsReference, narrowed));
    }

    private void WriteFailure(CurlExitCode expectedExitCode, string expectedMessage, SshTransferException exception)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ActFailure(exception);
        diagnostics.AssertFailure(expectedExitCode, expectedMessage, exception);
    }

    private void AssertDenied(SshOptions options, KnownHostsFile? knownHosts, string message)
    {
        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(() => Check(options, knownHosts));

        WriteFailure(CurlExitCode.PeerFailedVerification, message, exception);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(message, exception.Message);
    }
}
