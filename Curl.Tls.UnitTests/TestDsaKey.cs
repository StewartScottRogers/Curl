using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The DSA key of RFC 6979 appendix A.2.2 (p of 2048 bits, q of 256), for the in-memory
/// server's <c>DHE_DSS</c> credential: its <c>Dss-Parms</c>, its public key as the INTEGER
/// a certificate carries, and <c>Dss-Sig-Value</c> signatures made with <see cref="DsaSignature" />.
/// </summary>
internal static class TestDsaKey
{
    private static readonly byte[] Prime = Convert.FromHexString(
        "9DB6FB5951B66BB6FE1E140F1D2CE5502374161FD6538DF1648218642F0B5C48C8F7A41AADFA187324B87674FA1822B00F1ECF8136943D7C55757264E5A1A44F"
        + "FE012E9936E00C1D3E9310B01C7D179805D3058B2A9F4BB6F9716BFE6117C6B5B3CC4D9BE341104AD4A80AD6C94E005F4B993E14F091EB51743BF33050C38DE2"
        + "35567E1B34C3D6A5C0CEAA1A0F368213C3D19843D0B4B09DCB9FC72D39C8DE41F1BF14D4BB4563CA28371621CAD3324B6A2D392145BEBFAC748805236F5CA2FE"
        + "92B871CD8F9C36D3292B5509CA8CAA77A2ADFC7BFD77DDA6F71125A7456FEA153E433256A2261C6A06ED3693797E7995FAD5AABBCFBE3EDA2741E375404AE25B");

    private static readonly byte[] Subprime = Convert.FromHexString("F2C3119374CE76C9356990B465374A17F23F9ED35089BD969F61C6DDE9998C1F");

    private static readonly byte[] Generator = Convert.FromHexString(
        "5C7FF6B06F8F143FE8288433493E4769C4D988ACE5BE25A0E24809670716C613D7B0CEE6932F8FAA7C44D2CB24523DA53FBE4F6EC3595892D1AA58C4328A06C4"
        + "6A15662E7EAA703A1DECF8BBB2D05DBE2EB956C142A338661D10461C0D135472085057F3494309FFA73C611F78B32ADBB5740C361C9F35BE90997DB2014E2EF5"
        + "AA61782F52ABEB8BD6432C4DD097BC5423B285DAFB60DC364E8161F4A2A35ACA3A10B1C4D203CC76A470A33AFDCBDD92959859ABD8B56E1725252D78EAC66E71"
        + "BA9AE3F1DD2487199874393CD4D832186800654760E1E34C09E4D155179F9EC0DC4473F996BDCE6EED1CABED8B6F116F7AD9CF505DF0F998E34AB27514B0FFE7");

    private static readonly byte[] PrivateKey = Convert.FromHexString("69C7548C21D0DFEA6B9A51C9EAD4E27C33D3B3F180316E5BCAB92C933F0E4DBC");

    private static readonly byte[] PublicKey = Convert.FromHexString(
        "667098C654426C78D7F8201EAC6C203EF030D43605032C2F1FA937E5237DBD949F34A0A2564FE126DC8B715C5141802CE0979C8246463C40E6B6BDAA2513FA61"
        + "1728716C2E4FD53BC95B89E69949D96512E873B9C8F8DFD499CC312882561ADECB31F658E934C0C197F2C4D96B05CBAD67381E7B768891E4DA3843D24D94CDFB"
        + "5126E9B8BF21E8358EE0E0A30EF13FD6A664C0DCE3731F7FB49A4845A4FD8254687972A2D382599C9BAC4E0ED7998193078913032558134976410B89D2C171D1"
        + "23AC35FD977219597AA7D15C1A9A428E59194F75C721EBCBCFAE44696A499AFA74E04299F132026601638CB87AB79190D4A0986315DA8EEC6561C938996BEADF");

    /// <summary>Returns the DER <c>Dss-Parms</c> SEQUENCE of p, q and g (RFC 3279 section 2.3.2).</summary>
    public static byte[] EncodeDomainParameters() => EncodeIntegers(Prime, Subprime, Generator);

    /// <summary>Returns the DER INTEGER y a certificate's <c>subjectPublicKey</c> carries.</summary>
    public static byte[] EncodePublicKey()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteInteger(Positive(PublicKey));
        return writer.Encode();
    }

    /// <summary>Returns the DER <c>Dss-Sig-Value</c> over <paramref name="content" /> hashed with <paramref name="hash" />.</summary>
    public static byte[] Sign(HashAlgorithmName hash, byte[] content)
    {
        using DsaSignature key = new(Prime, Subprime, Generator, PrivateKey);
        byte[] rs = new byte[key.SignatureLength];
        key.SignHash(DsaSignature.HashData(content, hash), hash, rs);
        return EncodeIntegers(rs[..(rs.Length / 2)], rs[(rs.Length / 2)..]);
    }

    /// <summary>Returns a DER SEQUENCE of the unsigned big-endian values as INTEGERs.</summary>
    public static byte[] EncodeIntegers(params byte[][] values)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (byte[] value in values)
            {
                writer.WriteInteger(Positive(value));
            }
        }

        return writer.Encode();
    }

    private static BigInteger Positive(byte[] value) => new(value, isUnsigned: true, isBigEndian: true);
}
