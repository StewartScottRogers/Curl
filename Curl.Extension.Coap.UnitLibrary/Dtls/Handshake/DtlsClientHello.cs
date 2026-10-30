using System.Buffers.Binary;
using System.Net;
using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// The DTLS 1.2 ClientHello body (RFC 6347 section 4.2.1): TLS 1.2's ClientHello with a cookie after
/// the session id. It offers TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 and
/// TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 (RFC 5289) and TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 (RFC 7251), the secp256r1 group, uncompressed points and the
/// signature schemes <see cref="DtlsServerKeyExchangeSignature" /> verifies, extended_master_secret (RFC 7627), and names a host
/// name target in server_name.
/// </summary>
internal static class DtlsClientHello
{
    /// <summary>TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256.</summary>
    public const ushort EcdheEcdsaWithAes128GcmSha256 = 0xC02B;

    /// <summary>TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8.</summary>
    public const ushort EcdheEcdsaWithAes128Ccm8 = 0xC0AE;

    /// <summary>TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256.</summary>
    public const ushort EcdheRsaWithAes128GcmSha256 = 0xC02F;

    /// <summary>Gets the cipher suites the client offers, in preference order.</summary>
    public static IReadOnlyList<ushort> CipherSuites { get; } = [EcdheEcdsaWithAes128GcmSha256, EcdheRsaWithAes128GcmSha256, EcdheEcdsaWithAes128Ccm8];

    /// <summary>Writes the ClientHello body with <paramref name="cookie" />, empty before a HelloVerifyRequest.</summary>
    public static byte[] Encode(byte[] random, byte[] cookie, string targetHost)
    {
        List<byte> body = [.. UInt16(DtlsRecordHeader.Dtls12Version), .. random, 0, (byte)cookie.Length, .. cookie];
        body.AddRange(UInt16((ushort)(2 * CipherSuites.Count)));
        foreach (ushort suite in CipherSuites)
        {
            body.AddRange(UInt16(suite));
        }

        body.AddRange([1, 0]);
        List<byte> extensions = [];
        foreach (TlsExtension extension in ExtensionsFor(targetHost))
        {
            extensions.AddRange([.. UInt16((ushort)extension.Type), .. UInt16((ushort)extension.Data.Length), .. extension.Data]);
        }

        body.AddRange([.. UInt16((ushort)extensions.Count), .. extensions]);
        return [.. body];
    }

    /// <summary>Gives the extensions the ClientHello carries; server_name only for a host name, not an IP address (RFC 6066 section 3).</summary>
    public static List<TlsExtension> ExtensionsFor(string targetHost)
    {
        List<TlsExtension> extensions =
        [
            SupportedGroupsExtension.Encode([TlsNamedGroup.Secp256r1]),
            EcPointFormatsExtension.Encode([0]),
            SignatureAlgorithmsExtension.Encode(DtlsServerKeyExchangeSignature.Schemes),
            ExtendedMasterSecretExtension.Encode(),
        ];
        if (!IPAddress.TryParse(targetHost, out _))
        {
            extensions.Add(ServerNameExtension.EncodeHostName(targetHost));
        }

        return extensions;
    }

    private static byte[] UInt16(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }
}
