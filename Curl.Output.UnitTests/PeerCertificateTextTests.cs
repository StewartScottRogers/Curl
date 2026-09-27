namespace Curl.Output;

/// <summary>
/// Pins <see cref="PeerCertificateText" />: the measured loopback chain byte for byte, and
/// certificates built here for the records and refusals the chain does not reach, which
/// follow curl 8.21.0's <c>Curl_extract_certinfo</c> and <c>Curl_parseX509</c>.
/// </summary>
[TestClass]
public sealed class PeerCertificateTextTests
{
    private const string EcPublicKeyOid = "1.2.840.10045.2.1";
    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";
    private const string RsaEncryptionOid = "1.2.840.113549.1.1.1";
    private const string DsaOid = "1.2.840.10040.4.1";
    private const string DhPublicNumberOid = "1.2.840.10046.2.1";

    [TestMethod]
    public void Format_MeasuredLoopbackChain_MatchesCurl()
    {
        var text = string.Concat(LoopbackChain.Certificates.Select(PeerCertificateText.Format));

        Assert.AreEqual(LoopbackChain.CertsText, text);
    }

    [TestMethod]
    public void Format_MinimalCertificate_PrintsEveryRecordAndThePem()
    {
        var der = new CertificateParts().Build();

        var expected =
            "Subject:CN=s\n" +
            "Issuer:CN=i\n" +
            "Version:2\n" +
            "Serial Number:7\n" +
            "Signature Algorithm:ecdsa-with-SHA256\n" +
            "Start Date:2026-09-27 05:19:08 GMT\n" +
            "Expire Date:2126-09-03 05:19:07 GMT\n" +
            "Public Key Algorithm:ecPublicKey\n" +
            "ECC Public Key:8\n" +
            "ecPublicKey:04:01:02:\n" +
            "Signature:0a:0b:\n" +
            "-----BEGIN CERTIFICATE-----\n" +
            string.Concat(Convert.ToBase64String(der).Chunk(64).Select(line => new string(line) + "\n")) +
            "-----END CERTIFICATE-----\n";
        Assert.AreEqual(expected, PeerCertificateText.Format(der));
    }

    [TestMethod]
    public void Format_WithoutVersion_PrintsVersionZero()
    {
        var text = PeerCertificateText.Format(new CertificateParts { Version = [] }.Build());

        StringAssert.Contains(text, "\nVersion:0\n");
    }

    [TestMethod]
    public void Format_ValueWithNul_EndsTheRecordThere()
    {
        var text = PeerCertificateText.Format(new CertificateParts { Subject = Der.Name(("2.5.4.3", Der.Element(0x05))) }.Build());

        StringAssert.StartsWith(text, "Subject:CN=\nIssuer:");
    }

    [TestMethod]
    public void Format_ValueEndingInLineFeed_GetsNoSecondOne()
    {
        var text = PeerCertificateText.Format(new CertificateParts { Subject = Der.Name(("2.5.4.3", Der.Utf8("s\n"))) }.Build());

        StringAssert.StartsWith(text, "Subject:CN=s\nIssuer:");
    }

    [TestMethod]
    public void Format_RsaKeyWithLeadingZero_PrintsItsBitsAndDropsTheZero()
    {
        var modulus = Der.Integer(0x00, 0x80, 0x01, 0x02, 0x03, 0x04);

        var text = PeerCertificateText.Format(WithKey(RsaEncryptionOid, [0x05, 0x00], Der.Sequenced(modulus, Der.Integer(0x01, 0x00, 0x01))));

        StringAssert.Contains(text, "\nRSA Public Key:40\nrsa(n):80:01:02:03:04:\nrsa(e):0x10001\n");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x7F }, "RSA Public Key:7\nrsa(n):127\n")]
    [DataRow(new byte[] { 0x00 }, "RSA Public Key:0\nrsa(n):0\n")]
    public void Format_RsaKeyOf32BitsOrFewer_KeepsTheModulusAsEncoded(byte[] modulus, string expected)
    {
        var text = PeerCertificateText.Format(WithKey(RsaEncryptionOid, [], Der.Sequenced(Der.Integer(modulus), Der.Integer(0x03))));

        StringAssert.Contains(text, "\n" + expected + "rsa(e):3\n");
    }

    [TestMethod]
    public void Format_DsaKeyWithEveryParameter_PrintsThemAndTheKey()
    {
        var parameters = Der.Sequenced(Der.Integer(0x0B), Der.Integer(0x0C), Der.Integer(0x0D));

        var text = PeerCertificateText.Format(WithKey(DsaOid, parameters, Der.Integer(0x0E)));

        StringAssert.Contains(text, "\nPublic Key Algorithm:dsa\ndsa(p):11\ndsa(q):12\ndsa(g):13\ndsa(pub_key):14\nSignature:");
    }

    [TestMethod]
    [DataRow(0, "")]
    [DataRow(1, "dsa(p):11\n")]
    [DataRow(2, "dsa(p):11\ndsa(q):12\n")]
    public void Format_DsaKeyMissingParameters_StopsWithoutTheKey(int parameterCount, string expected)
    {
        byte[][] present = [.. new[] { Der.Integer(0x0B), Der.Integer(0x0C) }.Take(parameterCount)];
        byte[] parameters = parameterCount == 0 ? [] : Der.Sequenced(present);

        var text = PeerCertificateText.Format(WithKey(DsaOid, parameters, Der.Integer(0x0E)));

        StringAssert.Contains(text, "\nPublic Key Algorithm:dsa\n" + expected + "Signature:");
    }

    [TestMethod]
    public void Format_DiffieHellmanKey_PrintsItsParametersAndTheKey()
    {
        var text = PeerCertificateText.Format(WithKey(DhPublicNumberOid, Der.Sequenced(Der.Integer(0x17), Der.Integer(0x02)), Der.Integer(0x05)));

        StringAssert.Contains(text, "\nPublic Key Algorithm:dhpublicnumber\ndh(p):23\ndh(g):2\ndh(pub_key):5\nSignature:");
    }

    [TestMethod]
    public void Format_KeyOfAnotherAlgorithm_PrintsNoKeyRecords()
    {
        var text = PeerCertificateText.Format(WithKey("1.2.3", [], Der.Integer(0x01)));

        StringAssert.Contains(text, "\nPublic Key Algorithm:1.2.3\nSignature:");
    }

    [TestMethod]
    public void Format_WithUniqueIdentifiersAndExtensions_PrintsAsWithout()
    {
        var plain = PeerCertificateText.Format(new CertificateParts().Build());
        var trailer = new[] { Der.Element(0x81, [0x00]), Der.Element(0x82, [0x00]), Der.Element(0xA3, Der.Sequenced()) };

        var text = PeerCertificateText.Format(new CertificateParts { Trailer = trailer }.Build());

        Assert.AreEqual(plain[..plain.IndexOf("-----BEGIN", StringComparison.Ordinal)], text[..text.IndexOf("-----BEGIN", StringComparison.Ordinal)]);
    }

    [TestMethod]
    [DataRow(0x82)]
    [DataRow(0xA3)]
    public void Format_WithOneOptionalField_PrintsTheRecords(int identifier)
    {
        byte[] field = identifier == 0xA3 ? Der.Element(0xA3, Der.Sequenced()) : Der.Element((byte)identifier, [0x00]);

        var text = PeerCertificateText.Format(new CertificateParts { Trailer = [field] }.Build());

        StringAssert.StartsWith(text, "Subject:CN=s\n");
    }

    [TestMethod]
    public void Format_WithIssuerUniqueIdentifierOnly_PrintsTheRecords()
    {
        var text = PeerCertificateText.Format(new CertificateParts { Trailer = [Der.Element(0x81, [0x00])] }.Build());

        StringAssert.StartsWith(text, "Subject:CN=s\n");
    }

    [TestMethod]
    public void Format_EmptyExtensionsWrapper_PrintsNothing()
    {
        Assert.AreEqual(string.Empty, PeerCertificateText.Format(new CertificateParts { Trailer = [Der.Element(0xA3)] }.Build()));
    }

    [TestMethod]
    public void Format_EcKeyShorterThanTwoBytes_PrintsNothing()
    {
        var parts = new CertificateParts { PublicKeyInfo = Der.Sequenced(Der.Sequenced(Der.Oid(EcPublicKeyOid)), Der.Element(0x03, [0x00])) };

        Assert.AreEqual(string.Empty, PeerCertificateText.Format(parts.Build()));
    }

    [TestMethod]
    public void Format_NotACertificate_PrintsNothing()
    {
        Assert.AreEqual(string.Empty, PeerCertificateText.Format(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x00 }));
    }

    [TestMethod]
    public void Format_LongCertificate_BreaksThePemEverySixtyFourCharacters()
    {
        var text = PeerCertificateText.Format(new CertificateParts { Signature = Der.Bits(new byte[100]) }.Build());

        var pemLines = text[text.IndexOf("-----BEGIN", StringComparison.Ordinal)..].Split('\n');
        Assert.AreEqual(64, pemLines[1].Length);
        Assert.IsLessThan(64, pemLines[^3].Length);
        Assert.AreEqual("-----END CERTIFICATE-----", pemLines[^2]);
    }

    private static byte[] WithKey(string algorithmOid, byte[] parameters, byte[] key) =>
        new CertificateParts
        {
            PublicKeyInfo = Der.Sequenced(Der.Sequenced(Der.Oid(algorithmOid), parameters), Der.Bits(key)),
        }.Build();

    /// <summary>A certificate curl can read, each part replaceable.</summary>
    private sealed record CertificateParts
    {
        public byte[] Version { get; init; } = Der.Element(0xA0, Der.Integer(0x02));

        public byte[] Subject { get; init; } = Der.Name(("2.5.4.3", Der.Utf8("s")));

        public byte[] PublicKeyInfo { get; init; } = Der.Sequenced(Der.Sequenced(Der.Oid(EcPublicKeyOid)), Der.Bits(0x04, 0x01, 0x02));

        public byte[][] Trailer { get; init; } = [];

        public byte[] Signature { get; init; } = Der.Bits(0x0A, 0x0B);

        public byte[] Build()
        {
            var algorithm = Der.Sequenced(Der.Oid(EcdsaWithSha256Oid));
            var validity = Der.Sequenced(Der.Element(0x17, Der.Ascii("260927051908Z")), Der.Element(0x18, Der.Ascii("21260903051907Z")));
            var toBeSigned = Der.Sequenced(
                [Version, Der.Integer(0x07), algorithm, Der.Name(("2.5.4.3", Der.Utf8("i"))), validity, Subject, PublicKeyInfo, .. Trailer]);
            return Der.Sequenced(toBeSigned, algorithm, Signature);
        }
    }
}
