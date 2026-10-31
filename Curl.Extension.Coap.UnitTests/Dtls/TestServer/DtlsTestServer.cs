using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extension.Coap.Dtls.Record;
using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Extension.Coap.Dtls.TestServer;

/// <summary>
/// An in-memory DTLS 1.2 server for the client's handshake tests. A test hands it each datagram
/// the client sends with <see cref="Receive" /> and gets back the datagrams the server answers
/// with, one record each: a ClientHello without a valid cookie gets a HelloVerifyRequest, one
/// with it gets ServerHello, Certificate, ServerKeyExchange (ECDHE, signed) and
/// ServerHelloDone, the client's key exchange, ChangeCipherSpec and Finished get the server's
/// ChangeCipherSpec and Finished, and protected application data is echoed. A client that
/// retransmits gets the cached flight again. The server negotiates AES-128-GCM, or AES-128-CCM_8 when UseAes128Ccm8 is set, and the
/// plain master secret (it ignores extended_master_secret), and the handshake transcript holds
/// unfragmented messages with their DTLS headers (RFC 6347 section 4.2.6).
/// </summary>
internal sealed class DtlsTestServer : IDisposable
{
    private const int RandomLength = 32;

    private readonly DtlsTestServerKeyKind keyKind;

    private readonly TlsSigningKey signingKey;

    private readonly IDisposable keyToDispose;

    private readonly Dictionary<DtlsServerFlight, List<byte[]>> flights = [];

    private readonly HashSet<DtlsServerFlight> flightsToDrop = [];

    private readonly Dictionary<DtlsServerFlight, int> recordsToReplay = [];

    private readonly Dictionary<ushort, DtlsHandshakeReassembly> reassemblies = [];

    private readonly List<byte> transcript = [];

    private readonly DtlsReplayWindow clientReplayWindow = new();

    private ulong epochZeroSequenceNumber;

    private EcdhKeyShare? keyShare;

    private byte[] masterSecret = [];

    private ushort nextMessageSequence;

    private bool changeCipherSpecReceived;

    private DtlsRecordProtector? protector;

    private DtlsRecordUnprotector? unprotector;

    /// <summary>Creates a server with a freshly generated key and self-signed certificate.</summary>
    public DtlsTestServer(DtlsTestServerKeyKind keyKind)
    {
        this.keyKind = keyKind;
        CertificateRequest request;
        if (keyKind == DtlsTestServerKeyKind.Ecdsa)
        {
            ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            keyToDispose = ecdsa;
            signingKey = new EcdsaTlsSigningKey(ecdsa);
            request = new CertificateRequest("CN=dtls-test-server", ecdsa, HashAlgorithmName.SHA256);
        }
        else
        {
            RSA rsa = RSA.Create(2048);
            keyToDispose = rsa;
            signingKey = new RsaTlsSigningKey(rsa);
            request = new CertificateRequest("CN=dtls-test-server", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        CertificateDer = certificate.RawData;
    }

    /// <summary>Gets the DER of the server's self-signed certificate.</summary>
    public byte[] CertificateDer { get; }

    /// <summary>Gets the cookie the server accepts in a second ClientHello.</summary>
    public byte[] Cookie { get; } = [0xC0, 0x0C, 0x1E, 0x00, 0x11, 0x22, 0x33, 0x44];

    /// <summary>Gets the cipher suite the server picks.</summary>
    public ushort CipherSuite => UseAes128Ccm8 ? Handshake.DtlsClientHello.EcdheEcdsaWithAes128Ccm8 : keyKind == DtlsTestServerKeyKind.Ecdsa ? (ushort)0xC02B : (ushort)0xC02F;

    /// <summary>Gets or sets whether an ECDSA server picks TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 (RFC 7251) instead of the GCM suite.</summary>
    public bool UseAes128Ccm8 { get; set; }

    /// <summary>Gets the ClientHello random the server last accepted.</summary>
    public byte[] ClientRandom { get; private set; } = [];

    /// <summary>Gets or sets whether the server echoes extended_master_secret (RFC 7627) and derives its master secret from the session hash.</summary>
    public bool EchoExtendedMasterSecret { get; set; }

    /// <summary>Gets the ServerHello random the server sent.</summary>
    public byte[] ServerRandom { get; private set; } = [];

    /// <summary>Gets whether the server has verified the client's Finished.</summary>
    public bool HandshakeComplete { get; private set; }

    /// <summary>Gets the application data the server has decrypted, in arrival order.</summary>
    public List<byte[]> ReceivedApplicationData { get; } = [];

    /// <summary>Gets or sets the plaintext the server answers each application data record with; by default it echoes the record.</summary>
    public Func<byte[], byte[]> AnswerApplicationData { get; set; } = plaintext => plaintext;

    /// <summary>Makes the server send nothing the next time it would send <paramref name="flight" />; it still remembers the flight for a retransmission.</summary>
    public void DropNextFlight(DtlsServerFlight flight) => flightsToDrop.Add(flight);

    /// <summary>Makes the server send record <paramref name="recordIndex" /> of <paramref name="flight" /> a second time, as one more datagram, the next time it sends that flight.</summary>
    public void ReplayRecordOfNextFlight(DtlsServerFlight flight, int recordIndex) => recordsToReplay[flight] = recordIndex;

    /// <summary>Answers one datagram from the client.</summary>
    /// <returns>The datagrams to send back, in order; empty when the server stays silent.</returns>
    public IReadOnlyList<byte[]> Receive(byte[] datagram)
    {
        List<byte[]> answers = [];
        int offset = 0;
        while (offset < datagram.Length)
        {
            DtlsRecordHeader header = DtlsRecordHeader.Decode(datagram.AsSpan(offset));
            int recordLength = DtlsRecordHeader.HeaderLength + header.Length;
            byte[] record = datagram[offset..(offset + recordLength)];
            offset += recordLength;
            answers.AddRange(header.Epoch == 0 ? ReceiveEpochZeroRecord(header, record) : ReceiveProtectedRecord(record));
        }

        return answers;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        keyShare?.Dispose();
        protector?.Dispose();
        unprotector?.Dispose();
        keyToDispose.Dispose();
    }

    private static byte[] HandshakeFragmentOf(HandshakeType type, ushort messageSequence, byte[] body) =>
        new DtlsHandshakeFragment(new DtlsHandshakeHeader(type, body.Length, messageSequence, 0, body.Length), body).Encode();

    private static byte[] BodyOf(byte[] encodedHandshakeMessage) => encodedHandshakeMessage[HandshakeMessage.HeaderLength..];

    private IEnumerable<byte[]> ReceiveEpochZeroRecord(DtlsRecordHeader header, byte[] record)
    {
        byte[] fragment = record[DtlsRecordHeader.HeaderLength..];
        if (header.ContentType == TlsContentType.ChangeCipherSpec)
        {
            changeCipherSpecReceived = true;
            return [];
        }

        List<byte[]> answers = [];
        int offset = 0;
        while (header.ContentType == TlsContentType.Handshake && offset < fragment.Length)
        {
            DtlsHandshakeFragment handshake = DtlsHandshakeFragment.Decode(fragment.AsSpan(offset), out int bytesRead);
            offset += bytesRead;
            if (!reassemblies.TryGetValue(handshake.Header.MessageSequence, out DtlsHandshakeReassembly? reassembly))
            {
                reassembly = new DtlsHandshakeReassembly(handshake);
                reassemblies[handshake.Header.MessageSequence] = reassembly;
            }
            else
            {
                reassembly.Add(handshake);
            }

            if (reassembly.IsComplete)
            {
                reassemblies.Remove(handshake.Header.MessageSequence);
                answers.AddRange(ReceiveHandshakeMessage(reassembly.MessageType, reassembly.MessageSequence, reassembly.ToMessageBody()));
            }
        }

        return answers;
    }

    private List<byte[]> ReceiveHandshakeMessage(HandshakeType type, ushort messageSequence, byte[] body)
    {
        if (type == HandshakeType.ClientHello)
        {
            return ReceiveClientHello(messageSequence, body);
        }

        if (type != HandshakeType.ClientKeyExchange || keyShare is null)
        {
            throw new InvalidOperationException($"The test server did not expect handshake message {type}.");
        }

        if (unprotector is not null)
        {
            return flights.TryGetValue(DtlsServerFlight.Finished, out List<byte[]>? sent) ? [.. sent] : [];
        }

        transcript.AddRange(HandshakeFragmentOf(type, messageSequence, body));
        byte[] publicValue = Tls12ClientKeyExchange.Decode(body, Tls12KeyExchange.Ecdhe).Value.ExchangeKeys;
        byte[] preMasterSecret = keyShare.ComputeSharedSecret(publicValue[1..])
            ?? throw new InvalidOperationException("The client's ECDHE public value is not a point on the curve.");
        masterSecret = EchoExtendedMasterSecret
            ? TlsPrf.Sha256.ComputeExtendedMasterSecret(preMasterSecret, SHA256.HashData(transcript.ToArray()))
            : TlsPrf.Sha256.ComputeMasterSecret(preMasterSecret, ClientRandom, ServerRandom);
        Tls12BulkCipher bulkCipher = UseAes128Ccm8 ? Tls12BulkCipher.Aes128Ccm8 : Tls12BulkCipher.Aes128Gcm;
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(masterSecret, ClientRandom, ServerRandom, bulkCipher);
        unprotector = new DtlsRecordUnprotector(keys.ClientWrite, 1, bulkCipher);
        protector = new DtlsRecordProtector(keys.ServerWrite, 1, bulkCipher);
        return [];
    }

    /// <summary>Reads the ClientHello's random and cookie: version, random, session_id, cookie (RFC 6347 section 4.2.1).</summary>
    private List<byte[]> ReceiveClientHello(ushort messageSequence, byte[] body)
    {
        int sessionIdStart = 2 + RandomLength;
        int cookieStart = sessionIdStart + 1 + body[sessionIdStart];
        byte[] cookie = body[(cookieStart + 1)..(cookieStart + 1 + body[cookieStart])];
        if (!cookie.AsSpan().SequenceEqual(Cookie))
        {
            byte[] verifyRequest = new DtlsHelloVerifyRequest(DtlsRecordHeader.Dtls12Version, Cookie).Encode();
            return Emit(DtlsServerFlight.HelloVerifyRequest, [Epoch0Record(TlsContentType.Handshake, HandshakeFragmentOf(DtlsHelloVerifyRequest.MessageType, 0, verifyRequest))]);
        }

        if (flights.TryGetValue(DtlsServerFlight.ServerHello, out List<byte[]>? sent))
        {
            return [.. sent];
        }

        ClientRandom = body[2..(2 + RandomLength)];
        transcript.AddRange(HandshakeFragmentOf(HandshakeType.ClientHello, messageSequence, body));
        ServerRandom = RandomNumberGenerator.GetBytes(RandomLength);
        keyShare = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        ushort scheme = keyKind == DtlsTestServerKeyKind.Ecdsa ? TlsSignatureScheme.EcdsaSecp256r1Sha256 : TlsSignatureScheme.RsaPssRsaeSha256;
        Tls12ServerKeyExchange unsigned = new(new Tls12EcdheParameters(TlsNamedGroup.Secp256r1, keyShare.PublicKey), scheme, null);
        Tls12ServerKeyExchange keyExchange = unsigned with { Signature = signingKey.Sign(scheme, unsigned.BuildSignedContent(ClientRandom, ServerRandom)) };
        ServerHello hello = new(DtlsRecordHeader.Dtls12Version, ServerRandom, [], CipherSuite, 0, EchoExtendedMasterSecret ? [ExtendedMasterSecretExtension.Encode()] : []);
        nextMessageSequence = 1;
        List<byte[]> records =
        [
            HandshakeRecord(HandshakeType.ServerHello, BodyOf(hello.Encode())),
            HandshakeRecord(HandshakeType.Certificate, BodyOf(new Tls12CertificateMessage([CertificateDer]).Encode())),
            HandshakeRecord(HandshakeType.ServerKeyExchange, BodyOf(keyExchange.Encode())),
            HandshakeRecord(HandshakeType.ServerHelloDone, []),
        ];
        return Emit(DtlsServerFlight.ServerHello, records);
    }

    private IEnumerable<byte[]> ReceiveProtectedRecord(byte[] record)
    {
        if (unprotector is null || protector is null || !changeCipherSpecReceived)
        {
            return [];
        }

        if (!unprotector.TryUnprotect(record, out DtlsRecordHeader header, out byte[] plaintext))
        {
            return [];
        }

        if (clientReplayWindow.IsReplay(header.SequenceNumber))
        {
            bool resendFinished = header.ContentType == TlsContentType.Handshake && flights.ContainsKey(DtlsServerFlight.Finished);
            return resendFinished ? flights[DtlsServerFlight.Finished] : [];
        }

        clientReplayWindow.MarkReceived(header.SequenceNumber);
        if (header.ContentType == TlsContentType.ApplicationData)
        {
            ReceivedApplicationData.Add(plaintext);
            return [protector.Protect(TlsContentType.ApplicationData, AnswerApplicationData(plaintext))];
        }

        return header.ContentType == TlsContentType.Handshake ? ReceiveClientFinished(plaintext) : [];
    }

    private List<byte[]> ReceiveClientFinished(byte[] plaintext)
    {
        DtlsHandshakeFragment fragment = DtlsHandshakeFragment.Decode(plaintext, out _);
        byte[] expected = TlsPrf.Sha256.ComputeClientVerifyData(masterSecret, SHA256.HashData(transcript.ToArray()));
        if (fragment.Header.MessageType != HandshakeType.Finished || !fragment.Body.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidOperationException("The client's Finished verify_data does not match the handshake transcript.");
        }

        transcript.AddRange(plaintext);
        HandshakeComplete = true;
        byte[] verifyData = TlsPrf.Sha256.ComputeServerVerifyData(masterSecret, SHA256.HashData(transcript.ToArray()));
        byte[] changeCipherSpec = Epoch0Record(TlsContentType.ChangeCipherSpec, [1]);
        byte[] finished = protector!.Protect(TlsContentType.Handshake, HandshakeFragmentOf(HandshakeType.Finished, nextMessageSequence, verifyData));
        return Emit(DtlsServerFlight.Finished, [changeCipherSpec, finished]);
    }

    private byte[] HandshakeRecord(HandshakeType type, byte[] body)
    {
        byte[] fragment = HandshakeFragmentOf(type, nextMessageSequence++, body);
        transcript.AddRange(fragment);
        return Epoch0Record(TlsContentType.Handshake, fragment);
    }

    private byte[] Epoch0Record(TlsContentType contentType, byte[] fragment) =>
        [.. new DtlsRecordHeader(contentType, DtlsRecordHeader.Dtls12Version, 0, epochZeroSequenceNumber++, (ushort)fragment.Length).Encode(), .. fragment];

    private List<byte[]> Emit(DtlsServerFlight flight, List<byte[]> records)
    {
        flights[flight] = records;
        List<byte[]> toSend = flightsToDrop.Remove(flight) ? [] : [.. records];
        if (toSend.Count > 0 && recordsToReplay.Remove(flight, out int index))
        {
            toSend.Add(records[index]);
        }

        return toSend;
    }
}
