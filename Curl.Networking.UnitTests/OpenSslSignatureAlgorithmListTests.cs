namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="OpenSslSignatureAlgorithmList" /> reads <c>--sigalgs</c>, each row the
/// <c>signature_algorithms</c> list Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 sent for the value
/// (captured 2026-09-30, BL-709), or its exit 59 refusal.
/// </summary>
[TestClass]
public sealed class OpenSslSignatureAlgorithmListTests
{
    [TestMethod]
    [DataRow("ECDSA+SHA256", new ushort[] { 0x0403 })]
    [DataRow("rsa_pss_rsae_sha256:ECDSA+SHA256", new ushort[] { 0x0804, 0x0403 })]
    [DataRow("RSA+SHA256:RSA+SHA256", new ushort[] { 0x0401 })]
    [DataRow("rsa+sha256", new ushort[] { 0x0401 })]
    [DataRow(
        "RSA+SHA256:RSA-PSS+SHA256:PSS+SHA384:ECDSA+SHA384:ed25519:ed448:DSA+SHA256:RSA+SHA1:ECDSA+SHA1:RSA+SHA224",
        new ushort[] { 0x0401, 0x0804, 0x0805, 0x0503, 0x0807, 0x0808, 0x0402, 0x0301 })]
    [DataRow(
        "rsa_pss_pss_sha256:ecdsa_brainpoolP256r1tls13_sha256:mldsa65:rsa_pkcs1_sha512",
        new ushort[] { 0x0809, 0x081a, 0x0905, 0x0601 })]
    [DataRow("RSA+SHA1", new ushort[0])]
    public void Parse_WithAnAcceptedValue_OffersTheMeasuredSchemes(string value, ushort[] expected)
    {
        var schemes = OpenSslSignatureAlgorithmList.Parse(value);

        Assert.IsNotNull(schemes);
        CollectionAssert.AreEqual(expected, schemes.ToArray());
    }

    // Measured exit 59: curl: (59) failed setting signature algorithms: '<value>'.
    [TestMethod]
    [DataRow("bogus")]
    [DataRow("RSA+SHA256:")]
    [DataRow("RSA+SHA256:bogus")]
    [DataRow("RSA+SHA256,ECDSA+SHA256")]
    [DataRow("")]
    public void Parse_WithARefusedValue_ReturnsNull(string value) =>
        Assert.IsNull(OpenSslSignatureAlgorithmList.Parse(value));

    [TestMethod]
    public void Parse_WithNull_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => OpenSslSignatureAlgorithmList.Parse(null!));
}
