using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// A DTLS 1.2 reference server written from RFC 6347 and RFC 5246 for <c>coaps://</c> (RFC 7252 section 9.1). It
/// answers a ClientHello without its cookie with a HelloVerifyRequest (RFC 6347 section 4.2.1), then completes a full
/// handshake under TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 on secp256r1 with the RSA certificate it is given,
/// sending each handshake message whole in its own record; after the handshake each protected application data
/// record is one datagram for the application server, and each of its answers goes back in one protected record.
/// </summary>
/// <param name="application">The server the protected datagrams are for, such as a <see cref="CoapReferenceServer" />.</param>
/// <param name="certificate">The server's RSA certificate with its private key.</param>
/// <param name="fault">How the hello flight, the server's Finished and the answers go on the wire; <see cref="DtlsReferenceFault.None" /> when omitted.</param>
public sealed class DtlsReferenceServer(IDatagramReferenceServer application, X509Certificate2 certificate, DtlsReferenceFault? fault = null) : IDatagramReferenceServer, IDisposable
{
    private const byte ClientHelloType = 1;
    private const byte ServerHelloType = 2;
    private const byte HelloVerifyRequestType = 3;
    private const byte CertificateType = 11;
    private const byte ServerKeyExchangeType = 12;
    private const byte ServerHelloDoneType = 14;
    private const byte FinishedType = 20;
    private const int HandshakeHeaderLength = 12;
    private const int RandomLength = 32;

    /// <summary>TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 (RFC 5289).</summary>
    public const ushort CipherSuite = 0xC02F;

    private readonly DtlsReferenceFault fault = fault ?? DtlsReferenceFault.None;
    private readonly Lock gate = new();
    private readonly List<DtlsReferenceRecord> received = [];
    private readonly List<byte[]> clientHelloCookies = [];
    private readonly List<byte> transcript = [];
    private readonly byte[] serverRandom = RandomNumberGenerator.GetBytes(RandomLength);
    private readonly ECDiffieHellman keyShare = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    private byte[] clientRandom = [];
    private byte[] masterSecret = [];
    private DtlsReferenceRecordCipher clientCipher = null!;
    private DtlsReferenceRecordCipher serverCipher = null!;
    private ulong plainSequenceNumber;
    private ulong protectedSequenceNumber;
    private ushort messageSequence;
    private List<byte[]>? helloFlight;

    /// <summary>Gets the cookie the HelloVerifyRequest carries.</summary>
    public byte[] Cookie { get; } = RandomNumberGenerator.GetBytes(16);

    /// <summary>Gets the records curl sent, in order, protected ones as they arrived.</summary>
    public IReadOnlyList<DtlsReferenceRecord> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Gets the cookie of each ClientHello curl sent, empty where it carried none.</summary>
    public IReadOnlyList<byte[]> ClientHelloCookies
    {
        get
        {
            lock (gate)
            {
                return [.. clientHelloCookies];
            }
        }
    }

    /// <summary>Gets a value indicating whether curl's Finished carried the verify_data of the handshake (RFC 5246 section 7.4.9).</summary>
    public bool ClientFinishedVerified { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
    {
        lock (gate)
        {
            List<ReadOnlyMemory<byte>> answers = [];
            foreach (DtlsReferenceRecord record in DtlsReferenceRecord.Split(datagram.Span))
            {
                received.Add(record);
                answers.AddRange(AnswerRecord(record).Select(answer => new ReadOnlyMemory<byte>(answer.Encode())));
            }

            return answers;
        }
    }

    /// <summary>Disposes the server's ECDHE key share.</summary>
    public void Dispose() => keyShare.Dispose();

    private static byte[] HandshakeMessage(byte type, ushort sequence, byte[] body)
    {
        byte[] message = new byte[HandshakeHeaderLength + body.Length];
        message[0] = type;
        WriteUInt24(message.AsSpan(1), body.Length);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(4), sequence);
        WriteUInt24(message.AsSpan(9), body.Length);
        body.CopyTo(message, HandshakeHeaderLength);
        return message;
    }

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }

    private static byte[] UInt24(int value)
    {
        byte[] bytes = new byte[3];
        WriteUInt24(bytes, value);
        return bytes;
    }

    private List<DtlsReferenceRecord> AnswerRecord(DtlsReferenceRecord record) => record.Epoch == 0
        ? AnswerPlainRecord(record)
        : AnswerProtectedRecord(record);

    private List<DtlsReferenceRecord> AnswerPlainRecord(DtlsReferenceRecord record)
    {
        if (record.ContentType == DtlsReferenceRecord.ChangeCipherSpec)
        {
            return [];
        }

        return record.Fragment[0] == ClientHelloType ? AnswerClientHello(record) : TakeClientKeyExchange(record.Fragment);
    }

    /// <summary>A ClientHello without this server's cookie gets a HelloVerifyRequest; with it, the server's hello flight (RFC 6347 section 4.2.1).</summary>
    private List<DtlsReferenceRecord> AnswerClientHello(DtlsReferenceRecord record)
    {
        byte[] message = record.Fragment;
        ReadOnlySpan<byte> body = message.AsSpan(HandshakeHeaderLength);
        int cookieAt = 2 + RandomLength + 1 + body[2 + RandomLength];
        byte[] cookie = body.Slice(cookieAt + 1, body[cookieAt]).ToArray();
        clientHelloCookies.Add(cookie);
        plainSequenceNumber = Math.Max(plainSequenceNumber, record.SequenceNumber);
        if (!cookie.AsSpan().SequenceEqual(Cookie))
        {
            return [PlainRecord(DtlsReferenceRecord.Handshake, HandshakeMessage(HelloVerifyRequestType, messageSequence++, [0xFE, 0xFF, (byte)Cookie.Length, .. Cookie]))];
        }

        // A ClientHello curl sends again because the flight was lost gets the same flight again (section 4.2.4).
        helloFlight ??= BuildHelloFlight(message);
        return [.. fault.SendHelloFlight(helloFlight, this)];
    }

    private List<byte[]> BuildHelloFlight(byte[] clientHello)
    {
        clientRandom = clientHello.AsSpan(HandshakeHeaderLength + 2, RandomLength).ToArray();
        transcript.AddRange(clientHello);
        return [
            FlightMessage(ServerHelloType, [0xFE, 0xFD, .. serverRandom, 0, CipherSuite >> 8, CipherSuite & 0xFF, 0, 0, 0]),
            FlightMessage(CertificateType, [.. UInt24(certificate.RawData.Length + 3), .. UInt24(certificate.RawData.Length), .. certificate.RawData]),
            FlightMessage(ServerKeyExchangeType, ServerKeyExchangeBody()),
            FlightMessage(ServerHelloDoneType, []),
        ];
    }

    /// <summary>The ECDHE parameters on secp256r1 and their rsa_pkcs1_sha256 signature over both randoms (RFC 8422 section 5.4).</summary>
    private byte[] ServerKeyExchangeBody()
    {
        ECPoint point = keyShare.ExportParameters(false).Q;
        byte[] parameters = [3, 0x00, 0x17, 65, 4, .. point.X!, .. point.Y!];
        using RSA key = certificate.GetRSAPrivateKey()!;
        byte[] signature = key.SignData([.. clientRandom, .. serverRandom, .. parameters], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return [.. parameters, 0x04, 0x01, (byte)(signature.Length >> 8), (byte)signature.Length, .. fault.ServerKeyExchangeSignature(signature)];
    }

    /// <summary>Takes the client's ECDHE public value and derives the master secret and key block (RFC 5246 sections 8.1 and 6.3, RFC 5288 section 3).</summary>
    private List<DtlsReferenceRecord> TakeClientKeyExchange(byte[] message)
    {
        transcript.AddRange(message);
        // RFC 8422 section 5.7: ecdh_Yc<1..255>, one length byte and the uncompressed point 04, X, Y.
        ReadOnlySpan<byte> point = message.AsSpan(HandshakeHeaderLength + 1, message[HandshakeHeaderLength]);
        using ECDiffieHellman client = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33].ToArray(), Y = point[33..].ToArray() },
        });
        byte[] preMasterSecret = keyShare.DeriveRawSecretAgreement(client.PublicKey);
        masterSecret = DtlsReferencePrf.Compute(preMasterSecret, "master secret", [.. clientRandom, .. serverRandom], 48);
        byte[] keyBlock = DtlsReferencePrf.Compute(masterSecret, "key expansion", [.. serverRandom, .. clientRandom], 40);
        clientCipher = new DtlsReferenceRecordCipher(keyBlock[..16], keyBlock[32..36]);
        serverCipher = new DtlsReferenceRecordCipher(keyBlock[16..32], keyBlock[36..40]);
        return [];
    }

    private List<DtlsReferenceRecord> AnswerProtectedRecord(DtlsReferenceRecord record)
    {
        byte[] plaintext = clientCipher.Open(record);
        if (record.ContentType == DtlsReferenceRecord.Handshake)
        {
            return AnswerClientFinished(plaintext);
        }

        return [.. application.Answer(plaintext).SelectMany(answer => fault.SendApplicationAnswer(answer.ToArray(), this))];
    }

    /// <summary>Checks the client's Finished and answers with ChangeCipherSpec and the server's Finished (RFC 5246 section 7.4.9).</summary>
    private List<DtlsReferenceRecord> AnswerClientFinished(byte[] message)
    {
        byte[] expected = DtlsReferencePrf.Compute(masterSecret, "client finished", SHA256.HashData([.. transcript]), 12);
        ClientFinishedVerified = CryptographicOperations.FixedTimeEquals(message.AsSpan(HandshakeHeaderLength), expected);
        transcript.AddRange(message);
        byte[] verifyData = DtlsReferencePrf.Compute(masterSecret, "server finished", SHA256.HashData([.. transcript]), 12);
        return [
            PlainRecord(DtlsReferenceRecord.ChangeCipherSpec, [1]),
            ProtectedRecord(DtlsReferenceRecord.Handshake, HandshakeMessage(FinishedType, messageSequence++, fault.ServerFinishedVerifyData(verifyData))),
        ];
    }

    /// <summary>Gives the next unprotected record of epoch 0.</summary>
    /// <param name="contentType">The record's content type.</param>
    /// <param name="fragment">The record's fragment.</param>
    /// <returns>The record, under the next epoch 0 sequence number.</returns>
    internal DtlsReferenceRecord PlainRecord(byte contentType, byte[] fragment) => new(contentType, 0, plainSequenceNumber++, fragment);

    /// <summary>Gives the next protected record of epoch 1.</summary>
    /// <param name="contentType">The record's content type.</param>
    /// <param name="plaintext">The record's plaintext.</param>
    /// <returns>The record, under the next epoch 1 sequence number.</returns>
    internal DtlsReferenceRecord ProtectedRecord(byte contentType, byte[] plaintext) => ProtectedRecordAt(contentType, protectedSequenceNumber++, plaintext);

    /// <summary>Gives a protected record of epoch 1 under a sequence number the caller chooses, even one already sent.</summary>
    /// <param name="contentType">The record's content type.</param>
    /// <param name="sequenceNumber">The record's sequence number.</param>
    /// <param name="plaintext">The record's plaintext.</param>
    /// <returns>The record.</returns>
    internal DtlsReferenceRecord ProtectedRecordAt(byte contentType, ulong sequenceNumber, byte[] plaintext) => serverCipher.Seal(contentType, 1, sequenceNumber, plaintext);

    private byte[] FlightMessage(byte type, byte[] body)
    {
        byte[] message = HandshakeMessage(type, messageSequence++, body);
        transcript.AddRange(message);
        return message;
    }
}
