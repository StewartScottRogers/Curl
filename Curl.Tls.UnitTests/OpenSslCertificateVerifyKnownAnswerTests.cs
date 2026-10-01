namespace Curl.Tls;

/// <summary>
/// Pins the ML-DSA, Ed448 and brainpool TLS 1.3 ECDSA checks to signatures another
/// implementation made (BL-1047): each TLS 1.3 server CertificateVerify in
/// <c>KnownAnswers/openssl-tls13-certificate-verify.txt</c> was signed by OpenSSL 3.5.5,
/// so a verifier that only agrees with its own signer fails here.
/// </summary>
[TestClass]
public sealed class OpenSslCertificateVerifyKnownAnswerTests
{
    private static readonly Dictionary<string, Dictionary<string, string>> Vectors = ReadVectors();

    [TestMethod]
    [DataRow("bp256", TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256)]
    [DataRow("bp384", TlsSignatureScheme.EcdsaBrainpoolP384r1Tls13Sha384)]
    [DataRow("bp512", TlsSignatureScheme.EcdsaBrainpoolP512r1Tls13Sha512)]
    [DataRow("ed448", TlsSignatureScheme.Ed448)]
    [DataRow("mldsa44", TlsSignatureScheme.MlDsa44)]
    [DataRow("mldsa65", TlsSignatureScheme.MlDsa65)]
    [DataRow("mldsa87", TlsSignatureScheme.MlDsa87)]
    public void VerifySignature_OpenSslServerCertificateVerify_Verifies(string caseName, int scheme)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(Hex(vector, "certificate"))!;

        Assert.AreEqual(scheme, Convert.ToUInt16(vector["scheme"], 16));
        Assert.IsNull(key.VerifySignature((ushort)scheme, Hex(vector, "content"), Hex(vector, "signature")));
    }

    [TestMethod]
    [DataRow("bp256", TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256)]
    [DataRow("ed448", TlsSignatureScheme.Ed448)]
    [DataRow("mldsa65", TlsSignatureScheme.MlDsa65)]
    public void VerifySignature_OpenSslSignatureOverAnotherTranscript_IsADecryptError(string caseName, int scheme)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(Hex(vector, "certificate"))!;
        byte[] content = Hex(vector, "content");
        content[^1] ^= 0x01;

        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature((ushort)scheme, content, Hex(vector, "signature")));
    }

    [TestMethod]
    public void EveryVector_SignsAServerCertificateVerify()
    {
        byte[] transcriptHash = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];
        byte[] expected = TlsSignatureScheme.BuildCertificateVerifyContent(server: true, transcriptHash);

        Assert.HasCount(7, Vectors);
        foreach (Dictionary<string, string> vector in Vectors.Values)
        {
            CollectionAssert.AreEqual(expected, Hex(vector, "content"));
        }
    }

    private static byte[] Hex(Dictionary<string, string> vector, string field) => Convert.FromHexString(vector[field]);

    private static Dictionary<string, Dictionary<string, string>> ReadVectors()
    {
        using Stream stream = typeof(OpenSslCertificateVerifyKnownAnswerTests).Assembly.GetManifestResourceStream("KnownAnswers.openssl-tls13-certificate-verify.txt")!;
        using StreamReader reader = new(stream);
        Dictionary<string, Dictionary<string, string>> vectors = [];
        Dictionary<string, string> current = [];
        while (reader.ReadLine() is string line)
        {
            string[] parts = line.Split(" = ", 2);
            if (parts[0] == "case")
            {
                current = [];
                vectors[parts[1]] = current;
            }
            else if (parts.Length == 2)
            {
                current[parts[0]] = parts[1];
            }
        }

        return vectors;
    }
}
