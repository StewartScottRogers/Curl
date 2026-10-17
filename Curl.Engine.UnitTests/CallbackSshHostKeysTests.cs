using System.Text;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins how <see cref="CallbackSshHostKeys" /> hands the SSH handler's host-key questions to libcurl's
/// <c>CURLOPT_SSH_KEYFUNCTION</c> and <c>CURLOPT_SSH_HOSTKEYFUNCTION</c> (ADR-0623): each match status
/// passes through by number, each answer comes back by number, and the key types are libcurl's.
/// </summary>
[TestClass]
public sealed class CallbackSshHostKeysTests
{
    private static readonly byte[] FoundKey = [0, 0, 0, 11, 1, 2, 3];

    [TestMethod]
    public void AcceptanceOfAndDecisionOf_NoCallback_AreNull()
    {
        Assert.IsNull(CallbackSshHostKeys.AcceptanceOf(null));
        Assert.IsNull(CallbackSshHostKeys.DecisionOf(null));
    }

    [TestMethod]
    [DataRow(SshKnownHostMatch.Match, SshKeyMatch.Ok, DisplayName = "CURLKHMATCH_OK")]
    [DataRow(SshKnownHostMatch.Mismatch, SshKeyMatch.Mismatch, DisplayName = "CURLKHMATCH_MISMATCH")]
    [DataRow(SshKnownHostMatch.Missing, SshKeyMatch.Missing, DisplayName = "CURLKHMATCH_MISSING")]
    public void DecisionOf_EachMatchStatus_ReachesTheCallbackAsLibcurlsMatch(SshKnownHostMatch match, SshKeyMatch expected)
    {
        SshKeyMatch? seen = null;
        Func<SshKnownHostQuery, SshKnownHostVerdict> decide = CallbackSshHostKeys.DecisionOf((_, _, m) => { seen = m; return SshKeyStatus.Fine; })!;

        decide(new SshKnownHostQuery("ssh-ed25519", FoundKey, match == SshKnownHostMatch.Missing ? null : "AAAA", match));

        Assert.AreEqual(expected, seen);
    }

    [TestMethod]
    [DataRow(SshKeyStatus.FineAddToFile, SshKnownHostVerdict.AcceptAndAddToFile, DisplayName = "CURLKHSTAT_FINE_ADD_TO_FILE")]
    [DataRow(SshKeyStatus.Fine, SshKnownHostVerdict.Accept, DisplayName = "CURLKHSTAT_FINE")]
    [DataRow(SshKeyStatus.Reject, SshKnownHostVerdict.Reject, DisplayName = "CURLKHSTAT_REJECT")]
    [DataRow(SshKeyStatus.Defer, SshKnownHostVerdict.Defer, DisplayName = "CURLKHSTAT_DEFER")]
    [DataRow(SshKeyStatus.FineReplace, SshKnownHostVerdict.AcceptAndReplaceInFile, DisplayName = "CURLKHSTAT_FINE_REPLACE")]
    public void DecisionOf_EachStatus_ComesBackAsTheSameVerdict(SshKeyStatus status, SshKnownHostVerdict expected)
    {
        Func<SshKnownHostQuery, SshKnownHostVerdict> decide = CallbackSshHostKeys.DecisionOf((_, _, _) => status)!;

        SshKnownHostVerdict verdict = decide(new SshKnownHostQuery("ssh-rsa", FoundKey, null, SshKnownHostMatch.Missing));

        Assert.AreEqual(expected, verdict);
    }

    [TestMethod]
    public void DecisionOf_AKnownEntry_GivesItsBase64TextAndTheFoundKeysBytes()
    {
        SshKey? known = null;
        SshKey? found = null;
        Func<SshKnownHostQuery, SshKnownHostVerdict> decide = CallbackSshHostKeys.DecisionOf((k, f, _) => { known = k; found = f; return SshKeyStatus.Reject; })!;

        decide(new SshKnownHostQuery("ecdsa-sha2-nistp384", FoundKey, "QUJD", SshKnownHostMatch.Mismatch));

        Assert.IsNotNull(known);
        Assert.IsNotNull(found);
        Assert.AreEqual("QUJD", Encoding.ASCII.GetString(known.Key.Span));
        Assert.AreEqual(SshKeyType.Ecdsa, known.KeyType);
        CollectionAssert.AreEqual(FoundKey, found.Key.ToArray());
        Assert.AreEqual(SshKeyType.Ecdsa, found.KeyType);
    }

    [TestMethod]
    public void DecisionOf_NoKnownEntry_GivesNoKnownKey()
    {
        SshKey? known = new(FoundKey, SshKeyType.Rsa);
        Func<SshKnownHostQuery, SshKnownHostVerdict> decide = CallbackSshHostKeys.DecisionOf((k, _, _) => { known = k; return SshKeyStatus.Fine; })!;

        decide(new SshKnownHostQuery("ssh-rsa", FoundKey, null, SshKnownHostMatch.Missing));

        Assert.IsNull(known);
    }

    [TestMethod]
    [DataRow(SshKeyMatch.Ok, true)]
    [DataRow(SshKeyMatch.Mismatch, false)]
    [DataRow(SshKeyMatch.Missing, false)]
    public void AcceptanceOf_TheCallbacksAnswer_AcceptsOnlyOk(SshKeyMatch answer, bool expected)
    {
        SshKeyType? seenType = null;
        byte[]? seenKey = null;
        Func<string, ReadOnlyMemory<byte>, bool> accepts = CallbackSshHostKeys.AcceptanceOf((type, key) => { seenType = type; seenKey = key.ToArray(); return answer; })!;

        bool accepted = accepts("ssh-dss", FoundKey);

        Assert.AreEqual(expected, accepted);
        Assert.AreEqual(SshKeyType.Dss, seenType);
        CollectionAssert.AreEqual(FoundKey, seenKey);
    }

    [TestMethod]
    [DataRow("ssh-rsa", SshKeyType.Rsa)]
    [DataRow("ssh-dss", SshKeyType.Dss)]
    [DataRow("ecdsa-sha2-nistp256", SshKeyType.Ecdsa)]
    [DataRow("ecdsa-sha2-nistp521", SshKeyType.Ecdsa)]
    [DataRow("ssh-ed25519", SshKeyType.Ed25519)]
    [DataRow("ssh-ed448", SshKeyType.Unknown)]
    [DataRow("rsa-sha2-256", SshKeyType.Unknown)]
    public void KeyTypeOf_EachName_IsLibcurlsKeyType(string name, SshKeyType expected) =>
        Assert.AreEqual(expected, CallbackSshHostKeys.KeyTypeOf(name));
}
