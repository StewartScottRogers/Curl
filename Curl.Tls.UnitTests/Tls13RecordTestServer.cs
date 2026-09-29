using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// Puts <see cref="Tls13TestServer" /> on the server end of a byte stream: it reads the
/// client's records, answers in records protected with its own <see cref="Tls13RecordProtection" />s,
/// and then reads, sends and updates keys on request, so a test drives
/// <see cref="Tls13ClientConnection" /> end to end without a network.
/// </summary>
internal sealed class Tls13RecordTestServer(Stream transport, Tls13TestServer server)
{
    private Tls13RecordProtection? reader;
    private Tls13RecordProtection? writer;
    private byte[] readSecret = [];
    private byte[] writeSecret = [];
    private bool skipUnreadable;

    /// <summary>Gets or sets a value indicating whether the server sends a compatibility change_cipher_spec after its ServerHello.</summary>
    public bool SendChangeCipherSpec { get; set; }

    /// <summary>Gets the number of change_cipher_spec records the client sent.</summary>
    public int ChangeCipherSpecsReceived { get; private set; }

    /// <summary>Gets the legacy record versions of the plaintext handshake records the client sent.</summary>
    public List<ushort> ClientHelloRecordVersions { get; } = [];

    private Tls13CipherSuite Suite => Tls13CipherSuite.Find(server.CipherSuite)!;

    /// <summary>Gets the early data the server accepted, in order.</summary>
    public List<byte> EarlyData { get; } = [];

    /// <summary>Gets the number of records skipped because they did not open under the handshake keys: rejected early data (RFC 8446 section 4.2.10).</summary>
    public int SkippedRecords { get; private set; }

    /// <summary>Runs the server's side of the handshake: accepted early data through EndOfEarlyData, then through checking the client's Finished.</summary>
    public async Task HandshakeAsync()
    {
        TestServerFlight flight = await SendServerHelloAsync();
        await SendAsync(TlsContentType.Handshake, flight.Handshake);
        if (server.EarlyDataAccepted)
        {
            await ReceiveEarlyDataAsync();
        }

        await ReceiveClientFinishedAsync();
    }

    /// <summary>Sends a NewSessionTicket from <see cref="Tls13TestServer.IssueTicket" />.</summary>
    public Task SendNewSessionTicketAsync(IReadOnlyList<TlsExtension>? extensions = null) =>
        SendAsync(TlsContentType.Handshake, server.IssueTicket(extensions));

    private async Task ReceiveEarlyDataAsync()
    {
        while (true)
        {
            Tls13RecordContent content = (await ReceiveAsync())!;
            if (content.Type == TlsContentType.Handshake)
            {
                server.ReceiveEndOfEarlyData(content.Content);
                InstallReader(server.ClientHandshakeTrafficSecret);
                return;
            }

            Assert.AreEqual(TlsContentType.ApplicationData, content.Type);
            EarlyData.AddRange(content.Content);
        }
    }

    /// <summary>Answers the ClientHello (through a HelloRetryRequest when there is one) with the ServerHello, and puts the handshake keys in force.</summary>
    public async Task<TestServerFlight> SendServerHelloAsync()
    {
        TestServerFlight flight = server.Answer(await ReceiveClientHelloAsync());
        if (flight.IsHelloRetryRequest)
        {
            await SendAsync(TlsContentType.Handshake, flight.ServerHello);
            flight = server.Answer(await ReceiveClientHelloAsync());
        }

        await SendAsync(TlsContentType.Handshake, flight.ServerHello);
        if (SendChangeCipherSpec)
        {
            await SendAsync(TlsContentType.ChangeCipherSpec, [1]);
        }

        InstallWriter(server.ServerHandshakeTrafficSecret);
        InstallReader(server.EarlyDataAccepted ? server.ClientEarlyTrafficSecret : server.ClientHandshakeTrafficSecret);
        skipUnreadable = server.EarlyDataOffered && !server.EarlyDataAccepted;
        return flight;
    }

    /// <summary>Reads the client's plaintext ClientHello record, skipping change_cipher_spec.</summary>
    public async Task<byte[]> ReceiveClientHelloAsync()
    {
        while (true)
        {
            byte[] record = (await ReadRecordAsync())!;
            if (record[0] == (byte)TlsContentType.ChangeCipherSpec)
            {
                ChangeCipherSpecsReceived++;
                continue;
            }

            if (record[0] == (byte)TlsContentType.ApplicationData)
            {
                // Early data sent with a first ClientHello that a HelloRetryRequest answered.
                SkippedRecords++;
                continue;
            }

            Assert.AreEqual((byte)TlsContentType.Handshake, record[0]);
            ClientHelloRecordVersions.Add(BinaryPrimitives.ReadUInt16BigEndian(record.AsSpan(1)));
            return record[5..];
        }
    }

    /// <summary>Checks the client's Finished and puts the application keys in force.</summary>
    public async Task ReceiveClientFinishedAsync()
    {
        Tls13RecordContent clientFlight = (await ReceiveAsync())!;
        Assert.AreEqual(TlsContentType.Handshake, clientFlight.Type);
        server.ReceiveClientFlight(clientFlight.Content);
        InstallWriter(server.ServerApplicationTrafficSecret);
        InstallReader(server.ClientApplicationTrafficSecret);
    }

    /// <summary>Protects and sends content, or sends it in plaintext before the server has keys.</summary>
    public Task SendAsync(TlsContentType type, byte[] content) =>
        SendRawAsync(writer is null ? Plaintext(type, content) : writer.Protect(type, content));

    public async Task SendRawAsync(byte[] records)
    {
        await transport.WriteAsync(records);
        await transport.FlushAsync();
    }

    /// <summary>Protects content without sending it, so a test can change the record before it goes.</summary>
    public byte[] Protect(TlsContentType type, byte[] content) => writer!.Protect(type, content);

    /// <summary>Sends a KeyUpdate, then moves the server's write keys on.</summary>
    public async Task SendKeyUpdateAsync(bool requestUpdate)
    {
        await SendAsync(TlsContentType.Handshake, new HandshakeMessage(HandshakeType.KeyUpdate, [requestUpdate ? (byte)1 : (byte)0]).Encode());
        InstallWriter(Suite.KeySchedule.DeriveNextApplicationTrafficSecret(writeSecret));
    }

    /// <summary>Sends a post-handshake CertificateRequest naming <paramref name="context" /> and returns it.</summary>
    public async Task<byte[]> SendCertificateRequestAsync(byte[] context)
    {
        byte[] request = server.CreatePostHandshakeCertificateRequest(context);
        await SendAsync(TlsContentType.Handshake, request);
        return request;
    }

    /// <summary>Reads the client's answer to <paramref name="request" /> and checks it under the client's application traffic secret in force.</summary>
    public async Task ReceiveCertificateRequestAnswerAsync(byte[] request, byte[] context)
    {
        Tls13RecordContent answer = (await ReceiveAsync())!;
        Assert.AreEqual(TlsContentType.Handshake, answer.Type);
        server.ReceivePostHandshakeAnswer(request, answer.Content, context, readSecret);
    }

    /// <summary>Moves the server's read keys on, as a client KeyUpdate asks.</summary>
    public void UpdateReadKeys() => InstallReader(Suite.KeySchedule.DeriveNextApplicationTrafficSecret(readSecret));

    /// <summary>Reads the client's next record other than change_cipher_spec, unprotected; <see langword="null" /> when the client closed the transport.</summary>
    public async Task<Tls13RecordContent?> ReceiveAsync()
    {
        while (true)
        {
            byte[]? record = await ReadRecordAsync();
            if (record is null)
            {
                return null;
            }

            if (record[0] == (byte)TlsContentType.ChangeCipherSpec)
            {
                CollectionAssert.AreEqual(new byte[] { 1 }, record[5..]);
                ChangeCipherSpecsReceived++;
                continue;
            }

            if (reader is null)
            {
                return new Tls13RecordContent((TlsContentType)record[0], record[5..]);
            }

            TlsDecodeResult<Tls13RecordContent> content = reader.Unprotect(record);
            if (!content.Succeeded && skipUnreadable)
            {
                // A skipped record does not count against the handshake keys' sequence numbers.
                SkippedRecords++;
                InstallReader(readSecret);
                continue;
            }

            skipUnreadable = false;
            return content.Value;
        }
    }

    /// <summary>Reads application data until <paramref name="length" /> bytes have arrived.</summary>
    public async Task<byte[]> ReceiveApplicationDataAsync(int length)
    {
        List<byte> received = [];
        while (received.Count < length)
        {
            Tls13RecordContent content = (await ReceiveAsync())!;
            Assert.AreEqual(TlsContentType.ApplicationData, content.Type);
            received.AddRange(content.Content);
        }

        return [.. received];
    }

    private static byte[] Plaintext(TlsContentType type, byte[] content)
    {
        byte[] record = new byte[5 + content.Length];
        record[0] = (byte)type;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(1), 0x0303);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(3), (ushort)content.Length);
        content.CopyTo(record, 5);
        return record;
    }

    private async Task<byte[]?> ReadRecordAsync()
    {
        byte[] header = new byte[5];
        if (await transport.ReadAtLeastAsync(header, 5, false) < 5)
        {
            return null;
        }

        byte[] record = new byte[5 + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        header.CopyTo(record, 0);
        await transport.ReadExactlyAsync(record.AsMemory(5));
        return record;
    }

    private void InstallWriter(byte[] secret)
    {
        writer = Tls13RecordProtection.Create(Suite, secret);
        writeSecret = secret;
    }

    private void InstallReader(byte[] secret)
    {
        reader = Tls13RecordProtection.Create(Suite, secret);
        readSecret = secret;
    }
}
