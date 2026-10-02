using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Tls;

namespace Curl.Networking.Fakes;

/// <summary>
/// An in-memory server that speaks only TLS 1.0 or only TLS 1.1, as a legacy server or
/// <c>openssl s_server -tls1</c> does, built from <c>Curl.Tls.UnitLibrary</c>'s public codecs,
/// PRF and record states (BL-714). It answers any ClientHello with <paramref name="version" />
/// and <c>TLS_RSA_WITH_AES_128_CBC_SHA</c> (<c>0x002f</c>), echoes an empty
/// <c>renegotiation_info</c> and nothing else, checks the client's Finished, and then reads and
/// sends application data, so a test completes a transfer through the hand-built client with no
/// network and no operating-system TLS stack.
/// </summary>
/// <param name="transport">The server's end of the byte stream.</param>
/// <param name="version">The one version the server speaks.</param>
/// <param name="rsaKey">The private key of <paramref name="certificate" />.</param>
/// <param name="certificate">The DER certificate the server sends.</param>
internal sealed class LegacyTlsTestServer(Stream transport, TlsProtocolVersion version, RSA rsaKey, byte[] certificate)
{
    private const ushort RsaWithAes128CbcSha = 0x002f;

    private readonly List<byte> _transcript = [];
    private readonly List<byte> _handshakeBuffer = [];
    private readonly Tls12CipherSuite _suite = Tls12CipherSuite.Find(RsaWithAes128CbcSha)!;
    private Tls12RecordReadState _reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);
    private Tls12RecordWriteState _writer = Tls12RecordWriteState.CreatePlaintext(version);

    /// <summary>Gets the version of every record the client sent, in order.</summary>
    public List<ushort> RecordVersions { get; } = [];

    /// <summary>Runs the server's side of a full handshake, through its Finished.</summary>
    public async Task HandshakeAsync()
    {
        var clientHello = await ReceiveHandshakeMessageAsync();
        var hello = ClientHello.Decode(clientHello.Body).Value;
        var serverRandom = RandomNumberGenerator.GetBytes(32);
        var parameters = _suite.RecordProtectionFor(version, encryptThenMac: false);
        var prf = _suite.PrfFor(version);

        var flight = Add(new ServerHello((ushort)version, serverRandom, RandomNumberGenerator.GetBytes(32), RsaWithAes128CbcSha, 0, [RenegotiationInfoExtension.Encode([])]).Encode())
            .Concat(Add(new Tls12CertificateMessage([certificate]).Encode()))
            .Concat(Add(new HandshakeMessage(HandshakeType.ServerHelloDone, []).Encode()));
        await SendAsync(TlsContentType.Handshake, [.. flight]);

        var clientKeyExchange = await ReceiveHandshakeMessageAsync();
        var encrypted = Tls12ClientKeyExchange.Decode(clientKeyExchange.Body, Tls12KeyExchange.Rsa).Value.ExchangeKeys;
        var masterSecret = prf.ComputeMasterSecret(rsaKey.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1), hello.Random, serverRandom);
        var keyBlock = Tls12KeyBlock.Partition(parameters, prf.ComputeKeyBlock(masterSecret, serverRandom, hello.Random, parameters.KeyBlockLength));

        var (changeCipherSpecType, _) = await ReceiveRecordAsync();
        Assert.AreEqual(TlsContentType.ChangeCipherSpec, changeCipherSpecType);
        _reader = Tls12RecordReadState.Create(parameters, keyBlock.ClientWrite);
        var expectedClientVerifyData = prf.ComputeClientVerifyData(masterSecret, HashTranscript());
        var finished = await ReceiveHandshakeMessageAsync();
        CollectionAssert.AreEqual(expectedClientVerifyData, finished.Body);

        await SendAsync(TlsContentType.ChangeCipherSpec, Tls12OutgoingMessage.ChangeCipherSpec.Bytes);
        _writer = Tls12RecordWriteState.Create(parameters, keyBlock.ServerWrite, SystemTlsRandomSource.Instance);
        await SendAsync(TlsContentType.Handshake, new Finished(prf.ComputeServerVerifyData(masterSecret, HashTranscript())).Encode());
    }

    /// <summary>Reads application data until <paramref name="length" /> bytes have arrived.</summary>
    public async Task<byte[]> ReceiveApplicationDataAsync(int length)
    {
        List<byte> received = [];
        while (received.Count < length)
        {
            var (type, content) = await ReceiveRecordAsync();
            Assert.AreEqual(TlsContentType.ApplicationData, type);
            received.AddRange(content);
        }

        return [.. received];
    }

    /// <summary>Protects and sends content under the server's current write state.</summary>
    public async Task SendAsync(TlsContentType type, byte[] content)
    {
        await transport.WriteAsync(_writer.Protect(type, content));
        await transport.FlushAsync();
    }

    // TLS 1.0 and 1.1 hash the handshake for Finished as MD5 then SHA-1 (RFC 4346 section 7.4.9).
    private byte[] HashTranscript() =>
        [.. MD5.HashData([.. _transcript]), .. SHA1.HashData([.. _transcript])];

    private byte[] Add(byte[] message)
    {
        _transcript.AddRange(message);
        return message;
    }

    // The next handshake message, read across records as the client split or joined them,
    // and added to the transcript.
    private async Task<HandshakeMessage> ReceiveHandshakeMessageAsync()
    {
        HandshakeMessageReadResult read;
        while ((read = HandshakeMessageReader.Read([.. _handshakeBuffer])).Message is null)
        {
            var (type, content) = await ReceiveRecordAsync();
            Assert.AreEqual(TlsContentType.Handshake, type);
            _handshakeBuffer.AddRange(content);
        }

        _transcript.AddRange(_handshakeBuffer.Take(read.BytesConsumed));
        _handshakeBuffer.RemoveRange(0, read.BytesConsumed);
        return read.Message;
    }

    private async Task<(TlsContentType Type, byte[] Content)> ReceiveRecordAsync()
    {
        var header = new byte[5];
        await transport.ReadExactlyAsync(header);
        var fragment = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        await transport.ReadExactlyAsync(fragment);
        var type = (TlsContentType)header[0];
        RecordVersions.Add(BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1)));
        return (type, _reader.Unprotect(type, fragment).Value);
    }
}
