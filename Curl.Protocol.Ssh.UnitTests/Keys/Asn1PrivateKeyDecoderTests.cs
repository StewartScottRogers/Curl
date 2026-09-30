using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="Asn1PrivateKeyDecoder" /> on structures written here, for the cases no
/// tool writes; the files tools write are pinned by <see cref="SshPrivateKeyReaderTests" />.
/// </summary>
[TestClass]
public sealed class Asn1PrivateKeyDecoderTests
{
    [TestMethod]
    public void ReadEcPrivateKey_NoCurveNamedAnywhere_Throws()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(new byte[32]);
        }

        Assert.ThrowsExactly<CryptographicException>(() => Asn1PrivateKeyDecoder.ReadEcPrivateKey(writer.Encode(), curveOid: null));
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

        Assert.IsNull(Asn1PrivateKeyDecoder.ReadPkcs8(writer.Encode()));
    }
}
