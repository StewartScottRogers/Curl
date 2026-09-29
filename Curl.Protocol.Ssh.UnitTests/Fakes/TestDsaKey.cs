using System.Formats.Asn1;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// RFC 6979 appendix A.2.1's 1024-bit DSA key as a user key, written by hand in the three
/// formats a DSA key file comes in, since no tool on the lane machine writes DSA keys for
/// SSH any more; its signatures are deterministic.
/// </summary>
internal static class TestDsaKey
{
    /// <summary>Gets p.</summary>
    internal static byte[] Prime { get; } = Convert.FromHexString(TestHostKey.DsaPrime);

    /// <summary>Gets q.</summary>
    internal static byte[] Subprime { get; } = Convert.FromHexString(TestHostKey.DsaSubprime);

    /// <summary>Gets g.</summary>
    internal static byte[] Generator { get; } = Convert.FromHexString(TestHostKey.DsaGenerator);

    /// <summary>Gets y.</summary>
    internal static byte[] PublicKey { get; } = Convert.FromHexString(TestHostKey.DsaPublicKey);

    /// <summary>Gets x.</summary>
    internal static byte[] PrivateKey { get; } = Convert.FromHexString(TestHostKey.DsaPrivateKey);

    /// <summary>Gets the <c>ssh-dss</c> public key blob.</summary>
    internal static byte[] PublicKeyBlob { get; } = Join(Name("ssh-dss"), Mpint(Prime), Mpint(Subprime), Mpint(Generator), Mpint(PublicKey));

    /// <summary>OpenSSL's traditional <c>DSA PRIVATE KEY</c> DER: version, p, q, g, y, x.</summary>
    internal static byte[] Traditional()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            foreach (byte[] value in new[] { Prime, Subprime, Generator, PublicKey, PrivateKey })
            {
                writer.WriteIntegerUnsigned(value);
            }
        }

        return writer.Encode();
    }

    /// <summary>PKCS #8 <c>PrivateKeyInfo</c> DER: id-dsa with Dss-Parms, and x alone.</summary>
    internal static byte[] Pkcs8()
    {
        AsnWriter x = new(AsnEncodingRules.DER);
        x.WriteIntegerUnsigned(PrivateKey);
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.10040.4.1");
                using (writer.PushSequence())
                {
                    writer.WriteIntegerUnsigned(Prime);
                    writer.WriteIntegerUnsigned(Subprime);
                    writer.WriteIntegerUnsigned(Generator);
                }
            }

            writer.WriteOctetString(x.Encode());
        }

        return writer.Encode();
    }

    /// <summary>An unencrypted <c>openssh-key-v1</c> body holding the key.</summary>
    internal static byte[] OpenSsh() =>
        OpenSshKeyFile.Body(PublicKeyBlob, Join(Name("ssh-dss"), Mpint(Prime), Mpint(Subprime), Mpint(Generator), Mpint(PublicKey), Mpint(PrivateKey)));
}
