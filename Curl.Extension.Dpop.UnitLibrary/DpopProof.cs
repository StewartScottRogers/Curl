using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Dpop;

/// <summary>
/// Builds a DPoP proof (RFC 9449 §4.2): a JWS compact serialisation whose header holds <c>typ</c> <c>dpop+jwt</c>, the
/// key's <c>alg</c> and its public <c>jwk</c>, and whose payload holds <c>jti</c>, <c>htm</c>, <c>htu</c> and
/// <c>iat</c>, with <c>ath</c> when an access token is sent and <c>nonce</c> when the server gave one.
/// </summary>
internal static class DpopProof
{
    /// <summary>Gives the proof for one request.</summary>
    /// <param name="key">The key that signs it.</param>
    /// <param name="request">The request it proves.</param>
    /// <returns>The proof, <c>header.payload.signature</c>, each part base64url without padding.</returns>
    public static string Create(DpopKey key, DpopRequest request)
    {
        var signingInput = Base64Url.EncodeToString(Json(writer => WriteHeader(writer, key))) + "." + Base64Url.EncodeToString(Json(writer => WritePayload(writer, request)));
        return signingInput + "." + Base64Url.EncodeToString(key.Sign(Encoding.ASCII.GetBytes(signingInput)));
    }

    /// <summary>Gives a token's <c>ath</c>: the base64url SHA-256 of its ASCII (RFC 9449 §4.2).</summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns>The hash.</returns>
    public static string AccessTokenHashOf(string accessToken) => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)));

    private static void WriteHeader(Utf8JsonWriter writer, DpopKey key)
    {
        writer.WriteString("typ", "dpop+jwt");
        writer.WriteString("alg", key.Algorithm);
        writer.WritePropertyName("jwk");
        key.WriteJwk(writer);
    }

    private static void WritePayload(Utf8JsonWriter writer, DpopRequest request)
    {
        writer.WriteString("jti", request.Id);
        writer.WriteString("htm", request.Method);
        writer.WriteString("htu", request.TargetUri);
        writer.WriteNumber("iat", request.IssuedAt.ToUnixTimeSeconds());
        WriteIfGiven(writer, "ath", request.AccessToken is { } token ? AccessTokenHashOf(token) : null);
        WriteIfGiven(writer, "nonce", request.Nonce);
    }

    private static void WriteIfGiven(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static byte[] Json(Action<Utf8JsonWriter> writeMembers)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
