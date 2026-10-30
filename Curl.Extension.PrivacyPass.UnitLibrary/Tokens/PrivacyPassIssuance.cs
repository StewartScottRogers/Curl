using System.Security.Cryptography;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// Gets a token for one <c>PrivateToken</c> challenge from an issuer and gives the <c>Authorization</c> value that
/// redeems it (RFC 9577 §2.2.1): builds the token input from a fresh nonce, the challenge's digest and the key's ID,
/// blinds it, POSTs the <c>TokenRequest</c> to the issuer's request URL, and turns the <c>TokenResponse</c> into the
/// token's authenticator. Token type <c>0x0001</c> (RFC 9578 §5) blinds by VOPRF(P-384, SHA-384)
/// (<see cref="VoprfP384Sha384"/>) and checks the issuer's proof; token type <c>0x0002</c> (RFC 9578 §6) blinds by
/// Blind RSA (<see cref="BlindRsa"/>) and checks the unblinded signature.
/// </summary>
/// <param name="client">Sends the request to the issuer.</param>
/// <param name="issuerRequestUrl">The issuer's request URL, the <c>issuer-request-uri</c> of RFC 9578 §4.</param>
/// <param name="randomBytes">Gives cryptographically random octets.</param>
internal sealed class PrivacyPassIssuance(PrivacyPassHttpClient client, Uri issuerRequestUrl, Func<int, byte[]> randomBytes)
{
    /// <summary>The privately verifiable token type: VOPRF(P-384, SHA-384), RFC 9578 §5.</summary>
    public const ushort VoprfTokenType = 0x0001;

    /// <summary>The publicly verifiable token type: Blind RSA (2048-bit), RFC 9578 §6.</summary>
    public const ushort BlindRsaTokenType = 0x0002;

    /// <summary>The media type of a <c>TokenRequest</c> (RFC 9578 §8.2).</summary>
    public const string RequestMediaType = "application/private-token-request";

    /// <summary>The media type of a <c>TokenResponse</c> (RFC 9578 §8.2).</summary>
    public const string ResponseMediaType = "application/private-token-response";

    private const int NonceLength = 32;

    /// <summary>
    /// Gets a token for <paramref name="header"/>: by VOPRF when its token type is <see cref="VoprfTokenType"/>, and
    /// otherwise by Blind RSA, the only other type the caller offers it (<see cref="BlindRsaTokenType"/>).
    /// </summary>
    /// <param name="header">The challenge and the issuer's key.</param>
    /// <param name="cancellationToken">Cancels the request to the issuer.</param>
    /// <returns>The <c>Authorization</c> field value, <c>PrivateToken token="..."</c>.</returns>
    /// <exception cref="PrivacyPassException">No token was issued.</exception>
    public Task<string> RedeemAsync(PrivateTokenChallengeHeader header, CancellationToken cancellationToken) =>
        header.Challenge.TokenType == VoprfTokenType
            ? RedeemVoprfAsync(header, cancellationToken)
            : RedeemBlindRsaAsync(header, cancellationToken);

    // RFC 9578 §5: the token-key is the 49-byte compressed pkS; the response is the evaluated element and its proof.
    private async Task<string> RedeemVoprfAsync(PrivateTokenChallengeHeader header, CancellationToken cancellationToken)
    {
        var key = P384Group.Decode(header.TokenKey)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The PrivateToken challenge's token-key is not a compressed P-384 public key.");
        var tokenInput = TokenInput(VoprfTokenType, header);
        var voprf = new VoprfP384Sha384(randomBytes);
        var response = await RequestTokenAsync(tokenInput, voprf.Blind(tokenInput), P384Group.ElementLength + VoprfP384Sha384.ProofLength, cancellationToken);
        var authenticator = voprf.Finalize(tokenInput, key, response[..P384Group.ElementLength], response[P384Group.ElementLength..])
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The issuer's token response's proof does not verify with the challenge's token-key.");
        return Authorization(tokenInput, authenticator);
    }

    // RFC 9578 §6: the token-key is an RSASSA-PSS SubjectPublicKeyInfo; the response is the blind signature.
    private async Task<string> RedeemBlindRsaAsync(PrivateTokenChallengeHeader header, CancellationToken cancellationToken)
    {
        var key = BlindRsaPublicKey.Decode(header.TokenKey)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The PrivateToken challenge's token-key is not a 2048-bit RSASSA-PSS public key.");
        var tokenInput = TokenInput(BlindRsaTokenType, header);
        var blindRsa = new BlindRsa(key, randomBytes);
        var blindSignature = await RequestTokenAsync(tokenInput, blindRsa.Blind(tokenInput), BlindRsaPublicKey.ModulusLength, cancellationToken);
        var authenticator = blindRsa.Finalize(tokenInput, blindSignature)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The issuer's token response does not verify with the challenge's token-key.");
        return Authorization(tokenInput, authenticator);
    }

    // token_type || nonce || challenge_digest || token_key_id, the token key's ID being SHA-256 of the token-key.
    private byte[] TokenInput(ushort tokenType, PrivateTokenChallengeHeader header) =>
        [(byte)(tokenType >> 8), (byte)tokenType, .. randomBytes(NonceLength), .. SHA256.HashData(header.Challenge.Encoded), .. SHA256.HashData(header.TokenKey)];

    private static string Authorization(byte[] tokenInput, byte[] authenticator) =>
        $"PrivateToken token=\"{Base64Url.Encode([.. tokenInput, .. authenticator])}\"";

    // RFC 9578 §5.1, §5.2, §6.1 and §6.2: the TokenRequest (token type, the key ID's last octet, the blinded message)
    // is POSTed and a 2xx's body is the responseLength-byte TokenResponse.
    private async Task<byte[]> RequestTokenAsync(byte[] tokenInput, byte[] blinded, int responseLength, CancellationToken cancellationToken)
    {
        byte[] tokenRequest = [tokenInput[0], tokenInput[1], tokenInput[^1], .. blinded];
        var result = await client.SendAsync(
            new PrivacyPassHttpRequest("POST", issuerRequestUrl, [new("Accept", ResponseMediaType), new("Content-Type", RequestMediaType)], tokenRequest),
            cancellationToken);
        var response = result.Response ?? throw new PrivacyPassException(result.ExitCode, result.Message!);
        if (response.StatusCode is < 200 or >= 300)
        {
            throw new PrivacyPassException(CurlExitCode.LoginDenied, $"The issuer answered {response.StatusCode} without a token.");
        }

        return response.Body.Length == responseLength
            ? response.Body
            : throw new PrivacyPassException(CurlExitCode.WeirdServerReply, $"The issuer's token response is {response.Body.Length} bytes, not {responseLength}.");
    }
}
