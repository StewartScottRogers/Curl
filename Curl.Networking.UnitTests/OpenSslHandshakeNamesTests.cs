using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

[TestClass]
public sealed class OpenSslHandshakeNamesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519, "x25519")]
    [DataRow(TlsNamedGroup.Secp256r1, "secp256r1")]
    [DataRow(TlsNamedGroup.X25519MlKem768, "X25519MLKEM768")]
    [DataRow((ushort)0x0a0a, null)]
    public void GroupName_OfAGroup_IsOpenSslsName(ushort group, string? expected)
    {
        Diagnostics.Arrange("group", group);

        var name = OpenSslHandshakeNames.GroupName(group);
        Diagnostics.Act("name", name);

        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    public void GroupName_OfNone_IsNull()
    {
        Diagnostics.Arrange("group", "none");

        var name = OpenSslHandshakeNames.GroupName(null);
        Diagnostics.Act("name", name);

        Assert.IsNull(name);
    }

    [TestMethod]
    [DataRow(0x0804, "1.2.840.113549.1.1.1", "RSASSA-PSS")]
    [DataRow(0x0806, "1.2.840.113549.1.1.1", "RSASSA-PSS")]
    [DataRow(0x0809, "1.2.840.113549.1.1.10", "RSASSA-PSS")]
    [DataRow(0x080b, "1.2.840.113549.1.1.10", "RSASSA-PSS")]
    [DataRow(0x0401, "1.2.840.113549.1.1.1", "rsaEncryption")]
    [DataRow(0x0807, "1.3.101.112", "ED25519")]
    [DataRow(0x0808, "1.3.101.113", "ED448")]
    [DataRow(0x0403, "1.2.840.10045.2.1", "id-ecPublicKey")]
    [DataRow(0x0803, "1.2.840.10045.2.1", "id-ecPublicKey")]
    [DataRow(0x080c, "1.2.3.4", null)]
    public void SignatureTypeName_OfASchemeAndKey_IsOpenSslsShortName(int scheme, string keyAlgorithmOid, string? expected)
    {
        Diagnostics.Arrange("scheme", scheme);
        Diagnostics.Arrange("key algorithm", keyAlgorithmOid);

        var name = OpenSslHandshakeNames.SignatureTypeName((ushort)scheme, keyAlgorithmOid);
        Diagnostics.Act("name", name);

        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    public void SignatureTypeName_OfALegacySignatureWithNoScheme_NamesItsKey()
    {
        Diagnostics.Arrange("scheme", "none (TLS 1.0 ServerKeyExchange)");

        var name = OpenSslHandshakeNames.SignatureTypeName(null, "1.2.840.113549.1.1.1");
        var none = OpenSslHandshakeNames.SignatureTypeName(null, null);
        Diagnostics.Act("name", name);

        Assert.AreEqual("rsaEncryption", name);
        Assert.IsNull(none);
    }
}
