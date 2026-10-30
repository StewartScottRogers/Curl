using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Extension.PrivacyPass.Http;
using Curl.Extension.PrivacyPass.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// Gets a publicly verifiable token for one <c>PrivateToken</c> challenge from an issuer (RFC 9578 §6) and gives the
/// <c>Authorization</c> value that redeems it (RFC 9577 §2.2.1): builds the token input from a fresh nonce, the
/// challenge's digest and the key's ID, blinds it (<see cref="BlindRsa"/>), POSTs the <c>TokenRequest</c> to the
/// issuer's request URL, and unblinds the <c>TokenResponse</c> into the token's authenticator.
/// </summary>
/// <param name="client">Sends the request to the issuer.</param>
/// <param name="issuerRequestUrl">The issuer's request URL, the <c>issuer-request-uri</c> of RFC 9578 §4.</param>
/// <param name="randomBytes">Gives cryptographically random octets.</param>
internal sealed class PrivacyPassIssuance(PrivacyPassHttpClient client, Uri issuerRequestUrl, Func<int, byte[]> randomBytes)
{
    /// <summary>The token type this issuance runs: Blind RSA (2048-bit), RFC 9578 §6.</summary>
    public const ushort BlindRsaTokenType = 0x0002;

    /// <summary>The media type of a <c>TokenRequest</c> (RFC 9578 §8.2).</summary>
    public const string RequestMediaType = "application/private-token-request";

    /// <summary>The media type of a <c>TokenResponse</c> (RFC 9578 §8.2).</summary>
    public const string ResponseMediaType = "application/private-token-response";

    private const int NonceLength = 32;

    /// <summary>Gets a token for <paramref name="header"/>, whose token type must be <see cref="BlindRsaTokenType"/>.</summary>
    /// <param name="header">The challenge and the issuer's key.</param>
    /// <param name="cancellationToken">Cancels the request to the issuer.</param>
    /// <returns>The <c>Authorization</c> field value, <c>PrivateToken token="..."</c>.</returns>
    /// <exception cref="PrivacyPassException">No token was issued.</exception>
    public async Task<string> RedeemAsync(PrivateTokenChallengeHeader header, CancellationToken cancellationToken)
    {
        var key = BlindRsaPublicKey.Decode(header.TokenKey)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The PrivateToken challenge's token-key is not a 2048-bit RSASSA-PSS public key.");
        var tokenKeyId = SHA256.HashData(header.TokenKey);
        byte[] tokenInput = [.. TokenType(), .. randomBytes(NonceLength), .. SHA256.HashData(header.Challenge.Encoded), .. tokenKeyId];
        var blindRsa = new BlindRsa(key, randomBytes);
        byte[] tokenRequest = [.. TokenType(), tokenKeyId[^1], .. blindRsa.Blind(tokenInput)];
        var blindSignature = await RequestBlindSignatureAsync(tokenRequest, cancellationToken);
        var authenticator = blindRsa.Finalize(tokenInput, blindSignature)
            ?? throw new PrivacyPassException(CurlExitCode.WeirdServerReply, "The issuer's token response does not verify with the challenge's token-key.");
        return $"PrivateToken token=\"{Base64Url.Encode([.. tokenInput, .. authenticator])}\"";
    }

    private static byte[] TokenType()
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, BlindRsaTokenType);
        return bytes;
    }

    // RFC 9578 §6.1 and §6.2: the TokenRequest is POSTed and a 200's body is the Nk-byte blind signature.
    private async Task<byte[]> RequestBlindSignatureAsync(byte[] tokenRequest, CancellationToken cancellationToken)
    {
        var result = await client.SendAsync(
            new PrivacyPassHttpRequest("POST", issuerRequestUrl, [new("Accept", ResponseMediaType), new("Content-Type", RequestMediaType)], tokenRequest),
            cancellationToken);
        var response = result.Response ?? throw new PrivacyPassException(result.ExitCode, result.Message!);
        if (response.StatusCode is < 200 or >= 300)
        {
            throw new PrivacyPassException(CurlExitCode.LoginDenied, $"The issuer answered {response.StatusCode} without a token.");
        }

        return response.Body.Length == BlindRsaPublicKey.ModulusLength
            ? response.Body
            : throw new PrivacyPassException(CurlExitCode.WeirdServerReply, $"The issuer's token response is {response.Body.Length} bytes, not {BlindRsaPublicKey.ModulusLength}.");
    }
}
