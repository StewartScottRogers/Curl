using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
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
        TranscriptTransferEvents events = new();
        byte[] dssKey = Join(Name("ssh-dss"), Name("key"));

        SshTransferException refused = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.Check(dssKey, Host, Port, new SshOptions(), KnownHostsFile.Parse(string.Empty), events));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.IsFalse(refused.IsVerboseLine);
        CollectionAssert.AreEqual(
            new[] { "* SSH: unsupported host key type for knownhosts check", "* SSH: knownhost check failed" },
            events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Md5GivenAndTheFileUnread_ReportsOnlyTheUnreadFile()
    {
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostsPath = "kh", HostPublicKeyMd5 = "00" };

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(
            SshAlgorithmPreferences.WindowsReference, Host, Port, options, KnownHostsFile.Parse("broken\nhost ssh-rsa AAAAAAAAAAAAAAAAAAAAAAAA\n"), events);

        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, narrowed);
        CollectionAssert.AreEqual(new[] { "* SSH: failed to read known hosts from kh" }, events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_NoPathGiven_NamesAnEmptyPath()
    {
        TranscriptTransferEvents events = new();

        SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, new SshOptions(), KnownHostsFile.Parse(string.Empty), events);

        CollectionAssert.AreEqual(new[] { "* SSH: did not find host '127.0.0.1' in ''" }, events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Insecure_ReportsNothing()
    {
        TranscriptTransferEvents events = new();

        SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, Port, new SshOptions(), null, events);

        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_Rsa1Entry_ReportsTheHostFoundBeforeFailing()
    {
        TranscriptTransferEvents events = new();
        KnownHostsFile knownHosts = KnownHostsFile.Parse("127.0.0.1 1024 35 1234567890123456789012345\n");

        Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.WindowsReference, Host, 22, new SshOptions { KnownHostsPath = "kh" }, knownHosts, events));

        CollectionAssert.AreEqual(new[] { "* SSH: found host '127.0.0.1' in 'kh'" }, events.Transcript);
    }
}
