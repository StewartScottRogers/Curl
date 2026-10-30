using System.Formats.Asn1;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Curl.Extension.PrivacyPass.Http;
using Curl.Protocol.Abstractions;
using Curl.Extension.PrivacyPass.Wire;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// The issuer side of RFC 9578 §6 for the tests: a 2048-bit RSA key, its <c>SubjectPublicKeyInfo</c> with the
/// <c>id-RSASSA-PSS</c> OID (§6.5), a challenge, and the blind signature <c>blinded^d mod n</c> (RFC 9474 §4.3 BlindSign).
/// </summary>
internal static class TestIssuer
{
    private static readonly RSAParameters Key = CreateKey();

    public static BigInteger Modulus { get; } = new(Key.Modulus, isUnsigned: true, isBigEndian: true);

    public static BigInteger Exponent { get; } = new(Key.Exponent, isUnsigned: true, isBigEndian: true);

    public static byte[] Spki { get; } = SubjectPublicKeyInfo(BlindRsaPublicKey.RsassaPssOid, Key.Modulus!, Key.Exponent!);

    // token_type 2, issuer_name "issuer.example", no redemption context, origin_info "origin.example".
    public static byte[] Challenge { get; } =
        [0x00, 0x02, 0x00, 0x0E, .. "issuer.example"u8, 0x00, 0x00, 0x0E, .. "origin.example"u8];

    public static string WwwAuthenticate =>
        $"PrivateToken challenge=\"{Base64Url.Encode(Challenge)}\", token-key=\"{Base64Url.Encode(Spki)}\", max-age=10";

    public static byte[] SubjectPublicKeyInfo(string oid, byte[] modulus, byte[] exponent)
    {
        var rsaPublicKey = new AsnWriter(AsnEncodingRules.DER);
        using (rsaPublicKey.PushSequence())
        {
            rsaPublicKey.WriteInteger(new BigInteger(modulus, isUnsigned: true, isBigEndian: true));
            rsaPublicKey.WriteInteger(new BigInteger(exponent, isUnsigned: true, isBigEndian: true));
        }

        var spki = new AsnWriter(AsnEncodingRules.DER);
        using (spki.PushSequence())
        {
            using (spki.PushSequence())
            {
                spki.WriteObjectIdentifier(oid);
            }

            spki.WriteBitString(rsaPublicKey.Encode());
        }

        return spki.Encode();
    }

    public static byte[] Sign(byte[] blinded)
    {
        var signature = BigInteger.ModPow(new BigInteger(blinded, isUnsigned: true, isBigEndian: true), new BigInteger(Key.D, isUnsigned: true, isBigEndian: true), Modulus)
            .ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[BlindRsaPublicKey.ModulusLength];
        signature.CopyTo(padded, padded.Length - signature.Length);
        return padded;
    }

    public static bool Verifies(byte[] message, byte[] signature)
    {
        using var rsa = RSA.Create(new RSAParameters { Modulus = Key.Modulus, Exponent = Key.Exponent });
        return rsa.VerifyData(message, signature, HashAlgorithmName.SHA384, RSASignaturePadding.Pss);
    }

    public static byte[] Response(int status, byte[] body) =>
        [.. Encoding.ASCII.GetBytes($"HTTP/1.1 {status} X\r\nContent-Type: application/private-token-response\r\nContent-Length: {body.Length}\r\n\r\n"), .. body];

    private static RSAParameters CreateKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(true);
    }
}

/// <summary>A fake issuer connection that answers the <c>TokenRequest</c> written to it with <see cref="TestIssuer.Sign"/> of its blinded message.</summary>
internal sealed class SigningIssuerConnection : IConnection
{
    private readonly MemoryStream written = new();
    private byte[]? response;
    private int position;

    public byte[] TokenRequest => written.ToArray()[^(BlindRsaPublicKey.ModulusLength + 3)..];

    public string Head => Encoding.ASCII.GetString(written.ToArray()[..^(BlindRsaPublicKey.ModulusLength + 3)]);

    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        response ??= TestIssuer.Response(200, TestIssuer.Sign(written.ToArray()[^BlindRsaPublicKey.ModulusLength..]));
        var count = Math.Min(buffer.Length, response.Length - position);
        response.AsMemory(position, count).CopyTo(buffer);
        position += count;
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A connector that opens <paramref name="connection"/> for any target.</summary>
/// <param name="connection">The connection.</param>
internal sealed class FixedConnector(IConnection connection) : IConnector
{
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(connection));
}
