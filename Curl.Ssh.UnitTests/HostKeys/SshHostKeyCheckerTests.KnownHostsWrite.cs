using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Ssh.HostKeys;

/// <summary>
/// Pins which <see cref="SshOptions.KnownHostDecision" /> answers make
/// <see cref="SshHostKeyChecker" /> hand back a known-hosts file to write, as libssh2.c's
/// <c>ssh_knownhost</c> writes one (ADR-0626).
/// </summary>
public sealed partial class SshHostKeyCheckerTests
{
    [TestMethod]
    public void Check_AcceptAndAddToFileForAMissingHost_AddsThePlainHostName()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshOptions options = new() { KnownHostDecision = _ => SshKnownHostVerdict.AcceptAndAddToFile };
        string expected =
            $"[127.0.0.2]:2222 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n" +
            $"127.0.0.1 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n";

        string? written = SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Absent)?.ToText("\n");

        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void Check_AcceptAndReplaceInFileForAMismatch_ReplacesTheMismatchingEntry()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshOptions options = new() { KnownHostDecision = _ => SshKnownHostVerdict.AcceptAndReplaceInFile };
        string expected = $"127.0.0.1 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n";

        string? written = SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Mismatching)?.ToText("\n");

        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void Check_AcceptAndAddToFileForAMismatch_KeepsTheMismatchingEntry()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshOptions options = new() { KnownHostDecision = _ => SshKnownHostVerdict.AcceptAndAddToFile };
        string expected =
            $"[127.0.0.1]:2222 ecdsa-sha2-nistp256 {KnownHostsFileTests.OtherKeyBase64}\n" +
            $"127.0.0.1 ecdsa-sha2-nistp256 {KnownHostsFileTests.HostKeyBase64}\n";

        string? written = SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, Mismatching)?.ToText("\n");

        diagnostics.Act("written", written);
        diagnostics.Assert("written", expected, written);
        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    [DataRow(SshKnownHostVerdict.Accept, false, DisplayName = "CURLKHSTAT_FINE on a mismatch")]
    [DataRow(SshKnownHostVerdict.AcceptAndAddToFile, true, DisplayName = "CURLKHSTAT_FINE_ADD_TO_FILE on a match")]
    [DataRow(SshKnownHostVerdict.AcceptAndReplaceInFile, true, DisplayName = "CURLKHSTAT_FINE_REPLACE on a match")]
    public void Check_NothingToWrite_ReturnsNoFile(SshKnownHostVerdict verdict, bool matches)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshOptions options = new() { KnownHostDecision = _ => verdict };
        diagnostics.Arrange("verdict", verdict);
        diagnostics.Arrange("matches", matches);

        KnownHostsFile? rewritten = SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, matches ? Matching : Mismatching);

        diagnostics.Act("rewritten", rewritten is not null);
        diagnostics.Assert("rewritten", false, rewritten is not null);
        Assert.IsNull(rewritten);
    }
}
