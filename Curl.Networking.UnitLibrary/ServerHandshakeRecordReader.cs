using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Reads the server-to-client bytes of a TLS handshake, in any chunking, and names the
/// key-exchange group the server chose as OpenSSL's <c>SSL_get0_group_name</c> names it
/// (BL-4039). It joins the payloads of plaintext handshake records (content type 22), skips
/// other plaintext records, and stops at the first ChangeCipherSpec (20) or application data
/// (23) record, after which the handshake is encrypted, or once it has taken 64 KiB. TLS 1.3's
/// group is the first two bytes of the ServerHello's <c>key_share</c> extension, which are
/// also a HelloRetryRequest's <c>selected_group</c>; TLS 1.2's is the named curve of a
/// ServerKeyExchange whose <c>curve_type</c> is 3 (named_curve). It also names the server's
/// signature type as <c>OBJ_nid2sn(SSL_get_peer_signature_type_nid)</c> does (BL-4040): TLS 1.2's
/// from the signature scheme after a ServerKeyExchange's ECDHE or DHE parameters, TLS 1.3's (a
/// ServerHello whose <c>supported_versions</c> is 0x0304, whose CertificateVerify is encrypted)
/// from the server certificate's key algorithm the caller passes. Truncated or malformed input
/// yields no name and never throws.
/// </summary>
internal sealed class ServerHandshakeRecordReader
{
    /// <summary>The most bytes the reader takes before it stops.</summary>
    public const int MaximumBytes = 64 * 1024;

    private const int RecordHeaderLength = 5;
    private const int HandshakeHeaderLength = 4;
    private const byte ChangeCipherSpecRecord = 20;
    private const byte HandshakeRecord = 22;
    private const byte ApplicationDataRecord = 23;
    private const byte ServerHelloMessage = 2;
    private const byte ServerKeyExchangeMessage = 12;
    private const ushort KeyShareExtension = 0x0033;
    private const ushort SupportedVersionsExtension = 0x002b;
    private const ushort Tls13Version = 0x0304;
    private const byte NamedCurveType = 3;
    private const int DhParameterCount = 3;

    // OpenSSL's short name for the signature type NID of each TLS 1.2 signature scheme.
    private static readonly FrozenDictionary<ushort, string> NamesBySignatureScheme = new Dictionary<ushort, string>
    {
        [TlsSignatureScheme.RsaPssRsaeSha256] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPssRsaeSha384] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPssRsaeSha512] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPssPssSha256] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPssPssSha384] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPssPssSha512] = "RSASSA-PSS",
        [TlsSignatureScheme.RsaPkcs1Sha1] = "rsaEncryption",
        [TlsSignatureScheme.RsaPkcs1Sha256] = "rsaEncryption",
        [TlsSignatureScheme.RsaPkcs1Sha384] = "rsaEncryption",
        [TlsSignatureScheme.RsaPkcs1Sha512] = "rsaEncryption",
        [TlsSignatureScheme.EcdsaSha1] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaSecp256r1Sha256] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaSecp384r1Sha384] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaSecp521r1Sha512] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaBrainpoolP384r1Tls13Sha384] = "id-ecPublicKey",
        [TlsSignatureScheme.EcdsaBrainpoolP512r1Tls13Sha512] = "id-ecPublicKey",
        [TlsSignatureScheme.Ed25519] = "ED25519",
        [TlsSignatureScheme.Ed448] = "ED448",
    }.ToFrozenDictionary();

    // TLS 1.3 signs with RSA-PSS for every RSA key, so the signature type follows the key.
    private static readonly FrozenDictionary<string, string> Tls13NamesByKeyAlgorithm = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1.2.840.113549.1.1.1"] = "RSASSA-PSS",
        ["1.2.840.113549.1.1.10"] = "RSASSA-PSS",
        ["1.2.840.10045.2.1"] = "id-ecPublicKey",
        ["1.3.101.112"] = "ED25519",
        ["1.3.101.113"] = "ED448",
    }.ToFrozenDictionary();

    // OpenSSL 3.5's TLS group names, the first name of each group in its provider's table.
    private static readonly FrozenDictionary<ushort, string> NamesByGroup = new Dictionary<ushort, string>
    {
        [TlsNamedGroup.Secp256r1] = "secp256r1",
        [TlsNamedGroup.Secp384r1] = "secp384r1",
        [TlsNamedGroup.Secp521r1] = "secp521r1",
        [TlsNamedGroup.BrainpoolP256r1] = "brainpoolP256r1",
        [TlsNamedGroup.BrainpoolP384r1] = "brainpoolP384r1",
        [TlsNamedGroup.BrainpoolP512r1] = "brainpoolP512r1",
        [TlsNamedGroup.BrainpoolP256r1Tls13] = "brainpoolP256r1tls13",
        [TlsNamedGroup.BrainpoolP384r1Tls13] = "brainpoolP384r1tls13",
        [TlsNamedGroup.BrainpoolP512r1Tls13] = "brainpoolP512r1tls13",
        [TlsNamedGroup.X25519] = "x25519",
        [TlsNamedGroup.X448] = "x448",
        [TlsNamedGroup.Ffdhe2048] = "ffdhe2048",
        [TlsNamedGroup.Ffdhe3072] = "ffdhe3072",
        [TlsNamedGroup.Ffdhe4096] = "ffdhe4096",
        [TlsNamedGroup.Ffdhe6144] = "ffdhe6144",
        [TlsNamedGroup.Ffdhe8192] = "ffdhe8192",
        [TlsNamedGroup.MlKem512] = "MLKEM512",
        [TlsNamedGroup.MlKem768] = "MLKEM768",
        [TlsNamedGroup.MlKem1024] = "MLKEM1024",
        [TlsNamedGroup.SecP256r1MlKem768] = "SecP256r1MLKEM768",
        [TlsNamedGroup.X25519MlKem768] = "X25519MLKEM768",
        [TlsNamedGroup.SecP384r1MlKem1024] = "SecP384r1MLKEM1024",
    }.ToFrozenDictionary();

    private readonly byte[] received = new byte[MaximumBytes];
    private readonly List<byte> handshake = [];
    private int receivedCount;
    private int nextRecordAt;

    /// <summary>Gets a value indicating whether the reader takes no more bytes.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Gets OpenSSL's name for the group the handshake read so far chose, or
    /// <see langword="null" /> when it names none, names one OpenSSL has no name for, or is malformed.
    /// </summary>
    public string? GroupName =>
        FindLast(CollectionsMarshal.AsSpan(handshake), MessageGroup) is { } group && NamesByGroup.TryGetValue(group, out var name) ? name : null;

    private delegate ushort? MessageValueReader(byte messageType, ReadOnlySpan<byte> body);

    /// <summary>
    /// Names the type of the signature the server made, as OpenSSL's
    /// <c>OBJ_nid2sn(SSL_get_peer_signature_type_nid)</c> does.
    /// </summary>
    /// <param name="certificateKeyAlgorithmOid">
    /// The server certificate's public key algorithm OID, which names a TLS 1.3 signature because
    /// its CertificateVerify is encrypted, or <see langword="null" /> when there is none.
    /// </param>
    /// <returns>
    /// OpenSSL's short name, or <see langword="null" /> when the handshake read so far names no
    /// signature, names one OpenSSL's table here does not, or is malformed.
    /// </returns>
    public string? PeerSignatureTypeName(string? certificateKeyAlgorithmOid)
    {
        var messages = CollectionsMarshal.AsSpan(handshake);
        return FindLast(messages, MessageProtocolVersion) == Tls13Version
            ? Tls13SignatureTypeName(certificateKeyAlgorithmOid)
            : Tls12SignatureTypeName(FindLast(messages, MessageSignatureScheme));
    }

    private static string? Tls13SignatureTypeName(string? certificateKeyAlgorithmOid) =>
        certificateKeyAlgorithmOid is not null && Tls13NamesByKeyAlgorithm.TryGetValue(certificateKeyAlgorithmOid, out var name) ? name : null;

    private static string? Tls12SignatureTypeName(ushort? scheme) =>
        scheme is { } code && NamesBySignatureScheme.TryGetValue(code, out var name) ? name : null;

    /// <summary>Takes the next bytes the server sent; bytes after the reader stops are ignored.</summary>
    /// <param name="bytes">The bytes, in the order they arrived.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (IsComplete)
        {
            return;
        }

        var taken = Math.Min(bytes.Length, MaximumBytes - receivedCount);
        bytes[..taken].CopyTo(received.AsSpan(receivedCount));
        receivedCount += taken;
        ReadCompleteRecords();
        IsComplete |= receivedCount == MaximumBytes;
    }

    private void ReadCompleteRecords()
    {
        while (!IsComplete && receivedCount - nextRecordAt >= RecordHeaderLength)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(received.AsSpan(nextRecordAt + 3));
            var payloadAt = nextRecordAt + RecordHeaderLength;
            if (receivedCount - payloadAt < length)
            {
                return;
            }

            ReadRecord(received[nextRecordAt], received.AsSpan(payloadAt, length));
            nextRecordAt = payloadAt + length;
        }
    }

    private void ReadRecord(byte contentType, ReadOnlySpan<byte> payload)
    {
        IsComplete = contentType is ChangeCipherSpecRecord or ApplicationDataRecord;
        if (contentType == HandshakeRecord)
        {
            handshake.AddRange(payload);
        }
    }

    private static ushort? FindLast(ReadOnlySpan<byte> messages, MessageValueReader read)
    {
        ushort? value = null;
        while (messages.Length >= HandshakeHeaderLength)
        {
            var length = messages[1] << 16 | messages[2] << 8 | messages[3];
            if (messages.Length - HandshakeHeaderLength < length)
            {
                break;
            }

            value = read(messages[0], messages.Slice(HandshakeHeaderLength, length)) ?? value;
            messages = messages[(HandshakeHeaderLength + length)..];
        }

        return value;
    }

    private static ushort? MessageGroup(byte messageType, ReadOnlySpan<byte> body) => messageType switch
    {
        ServerHelloMessage => ServerHelloExtensionValue(body, KeyShareExtension),
        ServerKeyExchangeMessage => ServerKeyExchangeGroup(body),
        _ => null,
    };

    private static ushort? MessageProtocolVersion(byte messageType, ReadOnlySpan<byte> body) =>
        messageType == ServerHelloMessage ? ServerHelloExtensionValue(body, SupportedVersionsExtension) : null;

    private static ushort? MessageSignatureScheme(byte messageType, ReadOnlySpan<byte> body) =>
        messageType == ServerKeyExchangeMessage
            ? SignatureScheme(body, EcdheParametersLength(body)) ?? SignatureScheme(body, DheParametersLength(body))
            : null;

    private static ushort? ServerHelloExtensionValue(ReadOnlySpan<byte> body, ushort extensionType)
    {
        // legacy_version (2) and random (32), then session_id, cipher_suite (2), compression (1).
        const int SessionIdLengthAt = 2 + 32;
        if (body.Length <= SessionIdLengthAt)
        {
            return null;
        }

        var extensionsLengthAt = SessionIdLengthAt + 1 + body[SessionIdLengthAt] + 2 + 1;
        if (body.Length < extensionsLengthAt + 2)
        {
            return null;
        }

        var extensionsLength = BinaryPrimitives.ReadUInt16BigEndian(body[extensionsLengthAt..]);
        var extensions = body[(extensionsLengthAt + 2)..];
        return extensions.Length < extensionsLength ? null : ExtensionValue(extensions[..extensionsLength], extensionType);
    }

    // The first two bytes of the extension's data: key_share's group, supported_versions' version.
    private static ushort? ExtensionValue(ReadOnlySpan<byte> extensions, ushort extensionType)
    {
        while (extensions.Length >= 4)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(extensions);
            var length = BinaryPrimitives.ReadUInt16BigEndian(extensions[2..]);
            if (extensions.Length - 4 < length)
            {
                return null;
            }

            if (type == extensionType)
            {
                return length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(extensions[4..]) : null;
            }

            extensions = extensions[(4 + length)..];
        }

        return null;
    }

    private static ushort? ServerKeyExchangeGroup(ReadOnlySpan<byte> body) =>
        body.Length >= 3 && body[0] == NamedCurveType ? BinaryPrimitives.ReadUInt16BigEndian(body[1..]) : null;

    // curve_type (1), named_curve (2) and the public point (1-byte length); -1 when not ECDHE.
    private static int EcdheParametersLength(ReadOnlySpan<byte> body) =>
        body.Length >= 4 && body[0] == NamedCurveType ? 4 + body[3] : -1;

    // dh_p, dh_g and dh_Ys, each with a 2-byte length; -1 when they are cut short.
    private static int DheParametersLength(ReadOnlySpan<byte> body)
    {
        var length = 0;
        for (var parameter = 0; parameter < DhParameterCount; parameter++)
        {
            if (body.Length - length < 2)
            {
                return -1;
            }

            length += 2 + BinaryPrimitives.ReadUInt16BigEndian(body[length..]);
        }

        return length;
    }

    // The signature scheme (2) and signature (2-byte length) that must end the message exactly,
    // so ECDHE and DHE parameters are never mistaken for each other.
    private static ushort? SignatureScheme(ReadOnlySpan<byte> body, int parametersLength) =>
        parametersLength >= 0
            && body.Length - parametersLength >= 4
            && body.Length - parametersLength - 4 == BinaryPrimitives.ReadUInt16BigEndian(body[(parametersLength + 2)..])
            ? BinaryPrimitives.ReadUInt16BigEndian(body[parametersLength..])
            : null;
}
