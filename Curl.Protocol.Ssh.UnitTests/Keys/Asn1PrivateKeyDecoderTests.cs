using System.Formats.Asn1;
using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="Asn1PrivateKeyDecoder" /> on structures written here, for the cases no
/// tool writes; the files tools write are pinned by <see cref="SshPrivateKeyReaderTests" />.
/// </summary>
[TestClass]
public sealed class Asn1PrivateKeyDecoderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReadEcPrivateKey_NoCurveNamedAnywhere_Throws()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(new byte[32]);
        }

        Diagnostics.Bytes("ECPrivateKey", writer.Encode());
        Diagnostics.Arrange("curve OID", "(null)");

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Asn1PrivateKeyDecoder.ReadEcPrivateKey(writer.Encode(), curveOid: null));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void ReadPkcs8_UnknownAlgorithm_ReturnsNull()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.3.101.113");
            }

            writer.WriteOctetString(new byte[57]);
        }

        Diagnostics.Arrange("algorithm OID", "1.3.101.113 (Ed448)");
        Diagnostics.Bytes("PrivateKeyInfo", writer.Encode());

        SshPrivateKey? key = Asn1PrivateKeyDecoder.ReadPkcs8(writer.Encode());

        Diagnostics.ActKey(key);
        Diagnostics.Assert("key", "(none)", key?.KeyType ?? "(none)");
        Assert.IsNull(key);
    }
}
