using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="SshHostKeyChecker" /> gives curl 8.21.0's
/// known-hosts steps in the cases the whole-transfer tests cannot reach (BL-578, ADR-0262).
/// </summary>
public sealed partial class SshHostKeyCheckerTests
{
    [TestMethod]
    public void Check_HostKeyOfATypeKnownHostsCannotCompare_ReportsItUnsupportedAndRefusesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        byte[] dssKey = Join(Name("ssh-dss"), Name("key"));
        diagnostics.Bytes("host key", dssKey);
        diagnostics.Arrange("known hosts", "(empty file)");

        SshTransferException refused = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.Check(dssKey, Host, Port, new SshOptions(), KnownHostsFile.Parse(string.Empty), events));

        string[] expectedLines = ["* SSH: unsupported host key type for knownhosts check", "* SSH: knownhost check failed"];
        diagnostics.ActFailure(refused);
        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("exit code", SshAuthenticationDiagnostics.ExitCode(CurlExitCode.PeerFailedVerification), SshAuthenticationDiagnostics.ExitCode(refused.ExitCode));
        diagnostics.Assert("is a verbose line", false, refused.IsVerboseLine);
        diagnostics.AssertLines("verbose lines", expectedLines, events.Transcript);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.IsFalse(refused.IsVerboseLine);
        CollectionAssert.AreEqual(
            new[] { "* SSH: unsupported host key type for knownhosts check", "* SSH: knownhost check failed" },
            events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Md5GivenAndTheFileUnread_ReportsOnlyTheUnreadFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostsPath = "kh", HostPublicKeyMd5 = "00" };
        diagnostics.Arrange("options", "known hosts kh, MD5 00");
        diagnostics.Arrange("known hosts", "broken\\nhost ssh-rsa AAAAAAAAAAAAAAAAAAAAAAAA\\n");

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(
            SshAlgorithmPreferences.WindowsReference, Host, Port, options, KnownHostsFile.Parse("broken\nhost ssh-rsa AAAAAAAAAAAAAAAAAAAAAAAA\n"), events);

        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("same preferences as WindowsReference", true, ReferenceEquals(SshAlgorithmPreferences.WindowsReference, narrowed));
        diagnostics.AssertLines("verbose lines", ["* SSH: failed to read known hosts from kh"], events.Transcript);
        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, narrowed);
        CollectionAssert.AreEqual(new[] { "* SSH: failed to read known hosts from kh" }, events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_NoPathGiven_NamesAnEmptyPath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        diagnostics.Arrange("options", "no known hosts path");
        diagnostics.Arrange("known hosts", "(empty file)");

        SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, new SshOptions(), KnownHostsFile.Parse(string.Empty), events);

        diagnostics.ActLines(events.Transcript);
        diagnostics.AssertLines("verbose lines", ["* SSH: did not find host '127.0.0.1' in ''"], events.Transcript);
        CollectionAssert.AreEqual(new[] { "* SSH: did not find host '127.0.0.1' in ''" }, events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Insecure_ReportsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        diagnostics.Arrange("known hosts", "(none, -k)");

        SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, new SshOptions(), null, events);

        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("verbose line count", 0, events.Transcript.Count);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Rsa1Entry_ReportsTheHostFoundBeforeFailing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        KnownHostsFile knownHosts = KnownHostsFile.Parse("127.0.0.1 1024 35 1234567890123456789012345\n");
        diagnostics.Arrange("known hosts", "127.0.0.1 1024 35 1234567890123456789012345 (RSA1)");
        diagnostics.Arrange("host and port", $"{Host}:22");

        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, 22, new SshOptions { KnownHostsPath = "kh" }, knownHosts, events));

        diagnostics.ActFailure(failure);
        diagnostics.ActLines(events.Transcript);
        diagnostics.AssertLines("verbose lines", ["* SSH: found host '127.0.0.1' in 'kh'"], events.Transcript);
        CollectionAssert.AreEqual(new[] { "* SSH: found host '127.0.0.1' in 'kh'" }, events.Transcript);
    }
}
