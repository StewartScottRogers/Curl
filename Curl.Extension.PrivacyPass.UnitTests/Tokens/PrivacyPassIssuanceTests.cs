using System.Security.Cryptography;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>Pins the issuance of a token for each token type: VOPRF (RFC 9578 §5) and Blind RSA (RFC 9578 §6).</summary>
[TestClass]
public sealed class PrivacyPassIssuanceTests
{
    private const string Prefix = "PrivateToken token=\"";

    [TestMethod]
    public async Task RedeemAsync_VoprfChallenge_IssuesATokenWhoseAuthenticatorIsTheIssuersPrfOutput()
    {
        var issuer = new EvaluatingIssuerConnection();

        var authorization = await Issuance(issuer).RedeemAsync(VoprfTestIssuer.Header, CancellationToken.None);

        // §5.1: token_type 1 then the last byte of SHA-256(token-key).
        CollectionAssert.AreEqual(new byte[] { 0, 1, SHA256.HashData(VoprfTestIssuer.EncodedPublicKey)[^1] }, issuer.TokenRequest[..3]);
        StringAssert.StartsWith(authorization, Prefix);
        var token = Base64Url.Decode(authorization[Prefix.Length..^1])!;
        // §5.3: token_type, nonce(32), challenge_digest(32), token_key_id(32), authenticator(Nh = 48).
        Assert.HasCount(2 + 32 + 32 + 32 + VoprfP384Sha384.OutputLength, token);
        var tokenInput = token[..^VoprfP384Sha384.OutputLength];
        CollectionAssert.AreEqual(new byte[] { 0, 1 }, tokenInput[..2]);
        CollectionAssert.AreEqual(SHA256.HashData(VoprfTestIssuer.Challenge), tokenInput[34..66]);
        CollectionAssert.AreEqual(SHA256.HashData(VoprfTestIssuer.EncodedPublicKey), tokenInput[66..]);
        CollectionAssert.AreEqual(VoprfTestIssuer.Output(tokenInput), token[^VoprfP384Sha384.OutputLength..]);
    }

    [TestMethod]
    public async Task RedeemAsync_VoprfProofNotVerifying_FailsWithWeirdServerReply()
    {
        var exception = await Assert.ThrowsExactlyAsync<PrivacyPassException>(
            () => Issuance(new EvaluatingIssuerConnection(corruptProof: true)).RedeemAsync(VoprfTestIssuer.Header, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, exception.ExitCode);
    }

    [TestMethod]
    public async Task RedeemAsync_VoprfTokenKeyNotAP384Key_FailsWithWeirdServerReply()
    {
        var header = VoprfTestIssuer.Header with { TokenKey = [0x04, .. VoprfTestIssuer.EncodedPublicKey[1..]] };

        var exception = await Assert.ThrowsExactlyAsync<PrivacyPassException>(
            () => Issuance(new EvaluatingIssuerConnection()).RedeemAsync(header, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, exception.ExitCode);
    }

    [TestMethod]
    public async Task RedeemAsync_BlindRsaChallenge_StillIssuesAVerifyingToken()
    {
        var header = new PrivateTokenChallengeHeader(PrivateTokenChallenge.Decode(TestIssuer.Challenge)!, TestIssuer.Spki);

        var authorization = await Issuance(new SigningIssuerConnection()).RedeemAsync(header, CancellationToken.None);

        var token = Base64Url.Decode(authorization[Prefix.Length..^1])!;
        CollectionAssert.AreEqual(new byte[] { 0, 2 }, token[..2]);
        Assert.IsTrue(TestIssuer.Verifies(token[..^BlindRsaPublicKey.ModulusLength], token[^BlindRsaPublicKey.ModulusLength..]));
    }

    private static PrivacyPassIssuance Issuance(IConnection issuer) =>
        new(new PrivacyPassHttpClient(new FixedConnector(issuer)), new Uri("https://issuer.example/token-request"), RandomNumberGenerator.GetBytes);
}
