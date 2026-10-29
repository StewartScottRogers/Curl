using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Pins the signature scheme the server signed with, and the key type behind it, that the
/// TLS 1.3 handshake takes from the CertificateVerify and the TLS 1.2 handshake from the
/// ServerKeyExchange; RSA key exchange signs nothing and leaves both unset.
/// </summary>
[TestClass]
public sealed class PeerSignatureSchemeTests
{
    [TestMethod]
    public void Tls13HandshakeExposesTheRsaPssSchemeOfTheCertificateVerify()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client();

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, new Tls13TestServer(TestServerCredential.Rsa(TlsSignatureScheme.RsaPssRsaeSha256)));

        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>(TlsSignatureScheme.RsaPssRsaeSha256, client.PeerSignatureScheme);
        Assert.AreEqual(TlsSignatureScheme.RsaEncryptionOid, client.PeerSignatureKeyAlgorithmOid);
    }

    [TestMethod]
    public void Tls13HandshakeExposesTheEcdsaSchemeOfTheCertificateVerify()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client();

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, new Tls13TestServer(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256)));

        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>(TlsSignatureScheme.EcdsaSecp256r1Sha256, client.PeerSignatureScheme);
        Assert.AreEqual(TlsSignatureScheme.EcPublicKeyOid, client.PeerSignatureKeyAlgorithmOid);
    }

    [TestMethod]
    public void Tls12EcdheHandshakeExposesTheSchemeOfTheServerKeyExchange()
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPssRsaeSha256)) { CipherSuite = 0xc02f, SignatureScheme = TlsSignatureScheme.RsaPssRsaeSha256 };
        Tls12ClientHandshake client = Tls12HandshakeDriver.Client();

        Tls12HandshakeOutput output = Tls12HandshakeDriver.Run(client, server);

        Assert.IsTrue(output.IsComplete);
        Assert.AreEqual<ushort?>(TlsSignatureScheme.RsaPssRsaeSha256, client.PeerSignatureScheme);
        Assert.AreEqual(TlsSignatureScheme.RsaEncryptionOid, client.PeerSignatureKeyAlgorithmOid);
    }

    [TestMethod]
    public void Tls12RsaKeyExchangeLeavesThePeerSignatureUnset()
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { CipherSuite = 0x009c };
        Tls12ClientHandshake client = Tls12HandshakeDriver.Client();

        Tls12HandshakeOutput output = Tls12HandshakeDriver.Run(client, server);

        Assert.IsTrue(output.IsComplete);
        Assert.IsNull(client.PeerSignatureScheme);
        Assert.IsNull(client.PeerSignatureKeyAlgorithmOid);
    }
}
