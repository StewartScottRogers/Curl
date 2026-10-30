using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.ObliviousDoh.Wire;

namespace Curl.Extension.ObliviousDoh;

/// <summary>
/// The target's side of RFC 9230, written here from sections 6.3 and 6.4 apart from the library's client code:
/// a P-256 key, its configs, and an answer to a query message.
/// </summary>
internal sealed class TestTarget : IDisposable
{
    private readonly ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    internal byte[] PublicKey
    {
        get
        {
            ECParameters parameters = key.ExportParameters(false);
            return [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        }
    }

    internal byte[]? LastQueryPlaintext { get; private set; }

    internal ObliviousDohConfig Config(HpkeAead aead = HpkeAead.Aes128Gcm) => new(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, aead, PublicKey);

    // ObliviousDoHConfigs holding one version 0x0001 entry (section 5).
    internal byte[] ConfigsBytes(HpkeAead aead = HpkeAead.Aes128Gcm)
    {
        byte[] contents = Config(aead).EncodeContents();
        byte[] entry = [0x00, 0x01, .. Vector(contents)];
        return Vector(entry);
    }

    // Opens the query (section 6.3), asks answer for the DNS response, and seals it (section 6.4).
    internal byte[] Respond(byte[] queryMessage, Func<byte[], byte[]> answer, byte[]? responseNonce = null)
    {
        Assert.AreEqual((byte)0x01, queryMessage[0]);
        int keyIdLength = BinaryPrimitives.ReadUInt16BigEndian(queryMessage.AsSpan(1));
        byte[] keyId = queryMessage[3..(3 + keyIdLength)];
        byte[] encrypted = queryMessage[(3 + keyIdLength + 2)..];
        CollectionAssert.AreEqual(Config().KeyId(), keyId);

        Assert.IsTrue(Hpke.TrySetupBaseRecipient(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm, encrypted[..65], key.ExportParameters(true).D, "odoh query"u8, out HpkeContext? context));
        using (context)
        {
            byte[] plaintext = new byte[encrypted.Length - 65 - 16];
            Assert.IsTrue(context.TryOpen([0x01, .. Vector(keyId)], encrypted[65..], plaintext));
            LastQueryPlaintext = plaintext;
            int dnsLength = BinaryPrimitives.ReadUInt16BigEndian(plaintext);
            byte[] dnsResponse = answer(plaintext[2..(2 + dnsLength)]);

            byte[] nonce = responseNonce ?? RandomNumberGenerator.GetBytes(16);
            byte[] secret = new byte[16];
            context.Export("odoh response"u8, secret);
            byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, secret, [.. plaintext, .. Vector(nonce)]);
            byte[] aeadKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, "odoh key"u8.ToArray());
            byte[] aeadNonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, "odoh nonce"u8.ToArray());
            byte[] responsePlaintext = [.. Vector(dnsResponse), 0x00, 0x00];
            byte[] sealedText = new byte[responsePlaintext.Length];
            byte[] tag = new byte[16];
            using (var aesGcm = new AesGcm(aeadKey, 16))
            {
                aesGcm.Encrypt(aeadNonce, responsePlaintext, sealedText, tag, (byte[])[0x02, .. Vector(nonce)]);
            }

            return [0x02, .. Vector(nonce), .. Vector([.. sealedText, .. tag])];
        }
    }

    public void Dispose() => key.Dispose();

    internal static byte[] Vector(byte[] bytes) => [(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes];

    // A DNS response to query with one answer record: 2001:db8::1 for AAAA, 192.0.2.1 for A (RFC 1035 section 4.1).
    internal static byte[] AnswerTo(byte[] query)
    {
        bool aaaa = query[^3] == 28;
        byte[] address = aaaa ? [0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1] : [192, 0, 2, 1];
        byte[] response = [.. query, 0xC0, 0x0C, query[^4], query[^3], 0x00, 0x01, 0, 0, 0, 60, 0, (byte)address.Length, .. address];
        response[2] = 0x81;
        response[3] = 0x80;
        response[7] = 1;
        return response;
    }
}
