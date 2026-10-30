using Curl.Extension.PrivacyPass.Tokens;

namespace Curl.Extension.PrivacyPass.Wire;

/// <summary>Pins the <c>TokenChallenge</c> structure of RFC 9577 §2.1 and the <c>WWW-Authenticate</c> parameters of §2.1.1.</summary>
[TestClass]
public sealed class PrivateTokenChallengeTests
{
    [TestMethod]
    public void Decode_WellFormedChallenge_ReadsEveryField()
    {
        byte[] context = [.. Enumerable.Repeat((byte)7, 32)];
        byte[] encoded = [0x00, 0x02, 0x00, 0x01, (byte)'i', 0x20, .. context, 0x00, 0x01, (byte)'o'];

        var challenge = PrivateTokenChallenge.Decode(encoded);

        Assert.IsNotNull(challenge);
        Assert.AreEqual((ushort)2, challenge.TokenType);
        Assert.AreEqual("i", challenge.IssuerName);
        CollectionAssert.AreEqual(context, challenge.RedemptionContext);
        Assert.AreEqual("o", challenge.OriginInfo);
        Assert.AreSame(encoded, challenge.Encoded);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, DisplayName = "truncated token_type")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00 }, DisplayName = "truncated issuer_name length")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x05, 0x61 }, DisplayName = "issuer_name longer than the bytes")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x01, 0x61 }, DisplayName = "no redemption_context")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x01, 0x61, 0x00 }, DisplayName = "no origin_info")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x01, 0x61, 0x00, 0x00, 0x00, 0xFF }, DisplayName = "bytes after origin_info")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 }, DisplayName = "empty issuer_name")]
    [DataRow(new byte[] { 0x00, 0x02, 0x00, 0x01, 0x61, 0x01, 0x07, 0x00, 0x00 }, DisplayName = "redemption_context neither 0 nor 32 bytes")]
    public void Decode_MalformedChallenge_IsNull(byte[] encoded) => Assert.IsNull(PrivateTokenChallenge.Decode(encoded));

    [TestMethod]
    public void FindAll_FieldWithOtherSchemes_GivesOnlyThePrivateTokenChallengesThatDecode()
    {
        var field = $"Basic realm=\"a, b\", privatetoken challenge={Base64Url.Encode(TestIssuer.Challenge)}, token-key={Base64Url.Encode(TestIssuer.Spki)}, "
            + $"PrivateToken challenge=\"{Base64Url.Encode(TestIssuer.Challenge)}\", Negotiate, PrivateToken token-key=\"AA\", =x, flag, "
            + $"PrivateToken challenge=\"!!\", token-key=\"AA\", "
            + TestIssuer.WwwAuthenticate;

        var found = PrivateTokenChallengeHeader.FindAll(field).ToList();

        Assert.HasCount(2, found);
        CollectionAssert.AreEqual(TestIssuer.Spki, found[1].TokenKey);
        Assert.AreEqual("origin.example", found[0].Challenge.OriginInfo);
    }

    [TestMethod]
    public void FindAll_NoChallenge_IsEmpty() => Assert.IsEmpty(PrivateTokenChallengeHeader.FindAll("Basic realm=x"));

    [TestMethod]
    public void Base64UrlDecode_StandardBase64Alphabet_IsNull() => Assert.IsNull(Base64Url.Decode("a+b/"));

    [TestMethod]
    public void Base64UrlDecode_PaddedOrUnpadded_GivesTheSameBytes()
    {
        CollectionAssert.AreEqual(new byte[] { 0xFB, 0xFF }, Base64Url.Decode("-_8="));
        CollectionAssert.AreEqual(new byte[] { 0xFB, 0xFF }, Base64Url.Decode("-_8"));
    }
}
