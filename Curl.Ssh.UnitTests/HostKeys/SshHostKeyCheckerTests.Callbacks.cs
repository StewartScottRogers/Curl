using Curl.Protocol.Abstractions;
using Curl.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Ssh.HostKeys;

/// <summary>
/// Pins libcurl's two host-key callbacks as <see cref="SshHostKeyChecker" /> asks them (ADR-0623):
/// <see cref="SshOptions.KnownHostDecision" /> (<c>CURLOPT_SSH_KEYFUNCTION</c>) after the known-hosts
/// lookup, with the lookup's match and the entry's key text, and <see cref="SshOptions.HostKeyAcceptance" />
/// (<c>CURLOPT_SSH_HOSTKEYFUNCTION</c>) in place of the file.
/// </summary>
public sealed partial class SshHostKeyCheckerTests
{
    [TestMethod]
    [DataRow(SshKnownHostMatch.Match, DisplayName = "match")]
    [DataRow(SshKnownHostMatch.Mismatch, DisplayName = "mismatch")]
    [DataRow(SshKnownHostMatch.Missing, DisplayName = "missing")]
    public void Check_KnownHostDecision_IsAskedWithTheLookupsMatch(SshKnownHostMatch expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshKnownHostQuery? asked = null;
        SshOptions options = new() { KnownHostDecision = query => { asked = query; return SshKnownHostVerdict.Accept; } };
        diagnostics.Arrange("expected match", expected);

        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, FileFor(expected));

        diagnostics.Act("asked match", asked?.Match);
        diagnostics.Assert("asked match", expected, asked?.Match);
        Assert.IsNotNull(asked);
        Assert.AreEqual(expected, asked.Match);
        Assert.AreEqual("ecdsa-sha2-nistp256", asked.KeyTypeName);
        CollectionAssert.AreEqual(KnownHostsFileTests.HostKey, asked.FoundKey.ToArray());
        Assert.AreEqual(KnownKeyTextFor(expected), asked.KnownKeyText);
    }

    [TestMethod]
    [DataRow(SshKnownHostVerdict.Accept, DisplayName = "CURLKHSTAT_FINE")]
    [DataRow(SshKnownHostVerdict.AcceptAndAddToFile, DisplayName = "CURLKHSTAT_FINE_ADD_TO_FILE")]
    [DataRow(SshKnownHostVerdict.AcceptAndReplaceInFile, DisplayName = "CURLKHSTAT_FINE_REPLACE")]
    public void Check_KnownHostDecisionAcceptsAMismatch_AcceptsWithoutTheMatchLine(SshKnownHostVerdict verdict)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostDecision = _ => verdict };
        diagnostics.Arrange("verdict", verdict);

        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Mismatching, events);

        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("verbose line count", 1, events.Transcript.Count);
        Assert.HasCount(1, events.Transcript);
        StringAssert.StartsWith(events.Transcript[0], "* SSH: host check 1");
    }

    [TestMethod]
    [DataRow(SshKnownHostVerdict.Reject, DisplayName = "CURLKHSTAT_REJECT")]
    [DataRow(SshKnownHostVerdict.Defer, DisplayName = "CURLKHSTAT_DEFER")]
    [DataRow((SshKnownHostVerdict)99, DisplayName = "an unknown answer")]
    public void Check_KnownHostDecisionRefusesAMatch_IsExit60(SshKnownHostVerdict verdict)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostDecision = _ => verdict };
        diagnostics.Arrange("verdict", verdict);

        SshTransferException refused = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Matching, events));

        diagnostics.ActFailure(refused);
        diagnostics.ActLines(events.Transcript);
        diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.AreEqual(PeerFailedVerification, refused.Message);
        Assert.AreEqual("* SSH: knownhost check failed", events.Transcript[^1]);
    }

    [TestMethod]
    public void Check_KnownHostDecisionWithoutAKnownHostsFile_IsNotAsked()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        bool asked = false;
        SshOptions options = new() { KnownHostDecision = _ => { asked = true; return SshKnownHostVerdict.Reject; } };
        diagnostics.Arrange("known hosts", "(none, -k)");

        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, knownHosts: null);

        diagnostics.Act("asked", asked);
        diagnostics.Assert("asked", false, asked);
        Assert.IsFalse(asked);
    }

    [TestMethod]
    public void Check_HostKeyAcceptanceAccepts_AcceptsWhatTheFileWouldRefuse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string? askedType = null;
        byte[]? askedKey = null;
        SshOptions options = new() { HostKeyAcceptance = (type, key) => { askedType = type; askedKey = key.ToArray(); return true; } };
        diagnostics.Arrange("known hosts", "absent");

        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Absent);

        diagnostics.Act("asked type", askedType);
        diagnostics.Assert("asked type", "ecdsa-sha2-nistp256", askedType);
        Assert.AreEqual("ecdsa-sha2-nistp256", askedType);
        CollectionAssert.AreEqual(KnownHostsFileTests.HostKey, askedKey);
    }

    [TestMethod]
    public void Check_HostKeyAcceptanceRefuses_IsExit60EvenWhenTheFileMatches()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshOptions options = new() { HostKeyAcceptance = (_, _) => false };
        diagnostics.Arrange("known hosts", "matching");

        SshTransferException refused = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Matching));

        diagnostics.ActFailure(refused);
        diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, refused.ExitCode);
        Assert.AreEqual(PeerFailedVerification, refused.Message);
    }

    [TestMethod]
    public void Check_HostKeyAcceptanceWithAFingerprintGiven_IsNotAsked()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        bool asked = false;
        SshOptions options = new() { HostPublicKeyMd5 = Md5, HostKeyAcceptance = (_, _) => asked = true };
        diagnostics.Arrange("fingerprints", Fingerprints(options));

        SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Absent);

        diagnostics.Act("asked", asked);
        diagnostics.Assert("asked", false, asked);
        Assert.IsFalse(asked);
    }

    private static KnownHostsFile FileFor(SshKnownHostMatch match) => match switch
    {
        SshKnownHostMatch.Match => Matching,
        SshKnownHostMatch.Mismatch => Mismatching,
        _ => Absent,
    };

    private static string? KnownKeyTextFor(SshKnownHostMatch match) => match switch
    {
        SshKnownHostMatch.Match => KnownHostsFileTests.HostKeyBase64,
        SshKnownHostMatch.Mismatch => KnownHostsFileTests.OtherKeyBase64,
        _ => null,
    };
}
