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
/// ServerKeyExchange whose <c>curve_type</c> is 3 (named_curve). Truncated or malformed input
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
    private const byte NamedCurveType = 3;

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
        FindGroup(CollectionsMarshal.AsSpan(handshake)) is { } group && NamesByGroup.TryGetValue(group, out var name) ? name : null;

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

    private static ushort? FindGroup(ReadOnlySpan<byte> messages)
    {
        ushort? group = null;
        while (messages.Length >= HandshakeHeaderLength)
        {
            var length = messages[1] << 16 | messages[2] << 8 | messages[3];
            if (messages.Length - HandshakeHeaderLength < length)
            {
                break;
            }

            group = MessageGroup(messages[0], messages.Slice(HandshakeHeaderLength, length)) ?? group;
            messages = messages[(HandshakeHeaderLength + length)..];
        }

        return group;
    }

    private static ushort? MessageGroup(byte messageType, ReadOnlySpan<byte> body) => messageType switch
    {
        ServerHelloMessage => ServerHelloGroup(body),
        ServerKeyExchangeMessage => ServerKeyExchangeGroup(body),
        _ => null,
    };

    private static ushort? ServerHelloGroup(ReadOnlySpan<byte> body)
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
        return extensions.Length < extensionsLength ? null : KeyShareGroup(extensions[..extensionsLength]);
    }

    private static ushort? KeyShareGroup(ReadOnlySpan<byte> extensions)
    {
        while (extensions.Length >= 4)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(extensions);
            var length = BinaryPrimitives.ReadUInt16BigEndian(extensions[2..]);
            if (extensions.Length - 4 < length)
            {
                return null;
            }

            if (type == KeyShareExtension)
            {
                return length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(extensions[4..]) : null;
            }

            extensions = extensions[(4 + length)..];
        }

        return null;
    }

    private static ushort? ServerKeyExchangeGroup(ReadOnlySpan<byte> body) =>
        body.Length >= 3 && body[0] == NamedCurveType ? BinaryPrimitives.ReadUInt16BigEndian(body[1..]) : null;
}
