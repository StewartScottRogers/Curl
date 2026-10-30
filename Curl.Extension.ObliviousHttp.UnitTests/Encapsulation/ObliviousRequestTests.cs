using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

[TestClass]
public sealed class ObliviousRequestTests
{
    private static readonly byte[] BinaryRequest = Convert.FromHexString(Rfc9458Example.BinaryRequest);

    [TestMethod]
    public void Encapsulate_Rfc9458ExampleEphemeralKey_GivesItsEncapsulatedRequest()
    {
        using ObliviousRequest request = EncapsulateExample();

        Assert.AreEqual(Rfc9458Example.EncapsulatedRequest, Convert.ToHexStringLower(request.EncapsulatedRequest));
    }

    [TestMethod]
    public void DecapsulateResponse_Rfc9458ExampleResponse_GivesItsBinaryResponse()
    {
        using ObliviousRequest request = EncapsulateExample();

        byte[] binaryResponse = request.DecapsulateResponse(Convert.FromHexString(Rfc9458Example.EncapsulatedResponse));

        Assert.AreEqual(Rfc9458Example.BinaryResponse, Convert.ToHexStringLower(binaryResponse));
    }

    [TestMethod]
    public void DecapsulateResponse_WrongTag_ThrowsEncapsulatedResponseException()
    {
        using ObliviousRequest request = EncapsulateExample();
        byte[] encapsulatedResponse = Convert.FromHexString(Rfc9458Example.EncapsulatedResponse);
        encapsulatedResponse[^1] ^= 1;

        Assert.ThrowsExactly<EncapsulatedResponseException>(() => request.DecapsulateResponse(encapsulatedResponse));
    }

    [TestMethod]
    public void DecapsulateResponse_ShorterThanNonceAndTag_ThrowsEncapsulatedResponseException()
    {
        using ObliviousRequest request = EncapsulateExample();

        Assert.ThrowsExactly<EncapsulatedResponseException>(() => request.DecapsulateResponse(new byte[31]));
    }

    [TestMethod]
    [DataRow(HpkeAead.Aes128Gcm)]
    [DataRow(HpkeAead.Aes256Gcm)]
    [DataRow(HpkeAead.ChaCha20Poly1305)]
    public void Encapsulate_FreshEphemeralKey_GatewayOpensTheRequestAndItsResponseDecapsulates(HpkeAead aead)
    {
        KeyConfigurationChoice choice = Rfc9458Example.Choice() with { Suite = new HpkeSuite(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, aead) };
        using ObliviousRequest request = ObliviousRequest.Encapsulate(choice, BinaryRequest);
        byte[] binaryResponse = Convert.FromHexString(Rfc9458Example.BinaryResponse);

        byte[] encapsulatedResponse = Rfc9458Gateway.Respond(request.EncapsulatedRequest.ToArray(), aead, BinaryRequest, binaryResponse);

        CollectionAssert.AreEqual(binaryResponse, request.DecapsulateResponse(encapsulatedResponse));
    }

    [TestMethod]
    [DataRow(HpkeAead.Aes256Gcm)]
    [DataRow(HpkeAead.ChaCha20Poly1305)]
    public void DecapsulateResponse_WrongTagWith32ByteKey_ThrowsEncapsulatedResponseException(HpkeAead aead)
    {
        KeyConfigurationChoice choice = Rfc9458Example.Choice() with { Suite = new HpkeSuite(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, aead) };
        using ObliviousRequest request = ObliviousRequest.Encapsulate(choice, BinaryRequest);
        byte[] encapsulatedResponse = Rfc9458Gateway.Respond(request.EncapsulatedRequest.ToArray(), aead, BinaryRequest, [1, 0x40, 0xc8]);
        encapsulatedResponse[^1] ^= 1;

        Assert.ThrowsExactly<EncapsulatedResponseException>(() => request.DecapsulateResponse(encapsulatedResponse));
    }

    [TestMethod]
    public void Encapsulate_WrongLengthEphemeralKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ObliviousRequest.Encapsulate(Rfc9458Example.Choice(), BinaryRequest, new byte[31]));
    }

    [TestMethod]
    public void Encapsulate_NullChoice_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ObliviousRequest.Encapsulate(null!, BinaryRequest));
    }

    private static ObliviousRequest EncapsulateExample() =>
        ObliviousRequest.Encapsulate(Rfc9458Example.Choice(), BinaryRequest, Convert.FromHexString(Rfc9458Example.EphemeralPrivateKey));

    /// <summary>The gateway's side of RFC 9458 sections 4.3 and 4.4, written from the RFC to check the client against.</summary>
    private static class Rfc9458Gateway
    {
        internal static byte[] Respond(byte[] encapsulatedRequest, HpkeAead aead, byte[] expectedRequest, byte[] binaryResponse)
        {
            byte[] header = encapsulatedRequest[..7];
            byte[] encapsulatedKey = encapsulatedRequest[7..39];
            HpkeSuite suite = new(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, aead);
            using HpkeReceiver receiver = HpkeReceiver.SetUp(suite, encapsulatedKey, Convert.FromHexString(Rfc9458Example.GatewayPrivateKey), [.. "message/bhttp request"u8, 0, .. header]);
            CollectionAssert.AreEqual(expectedRequest, receiver.Open([], encapsulatedRequest[39..]));

            int keySize = aead == HpkeAead.Aes128Gcm ? 16 : 32;
            byte[] secret = receiver.Export("message/bhttp response"u8, keySize);
            byte[] responseNonce = RandomNumberGenerator.GetBytes(keySize);
            byte[] pseudorandomKey = HKDF.Extract(HashAlgorithmName.SHA256, secret, [.. encapsulatedKey, .. responseNonce]);
            byte[] key = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, keySize, "key"u8.ToArray());
            byte[] nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudorandomKey, 12, "nonce"u8.ToArray());
            byte[] sealedText = new byte[binaryResponse.Length];
            byte[] tag = new byte[16];
            if (aead == HpkeAead.ChaCha20Poly1305)
            {
                using AeadChaCha20Poly1305 chaCha20Poly1305 = new(key);
                chaCha20Poly1305.Encrypt(nonce, binaryResponse, sealedText, tag, []);
            }
            else
            {
                using AesGcm aesGcm = new(key, 16);
                aesGcm.Encrypt(nonce, binaryResponse, sealedText, tag);
            }

            return [.. responseNonce, .. sealedText, .. tag];
        }
    }
}
