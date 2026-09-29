using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// Puts <see cref="Tls12TestServer" /> on the server end of a byte stream: it reads the
/// client's records, answers in records under its own <see cref="Tls12RecordWriteState" />,
/// switches its read and write states at each ChangeCipherSpec, and then reads and sends on
/// request, so a test drives <see cref="Tls12ClientConnection" /> end to end without a network.
/// </summary>
internal sealed class Tls12RecordTestServer(Stream transport, Tls12TestServer server)
{
    private Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls12);
    private Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(server.Version);

    /// <summary>Gets the longest handshake content one record carries; shorter than a flight splits its messages across records.</summary>
    public int HandshakeRecordLength { get; init; } = Tls12RecordWriteState.MaximumFragmentLength;

    /// <summary>Gets the version of every record the client sent, in order.</summary>
    public List<ushort> RecordVersions { get; } = [];

    /// <summary>Gets the length of every application data record the client sent, in order.</summary>
    public List<int> ApplicationDataLengths { get; } = [];

    /// <summary>Runs the server's side of a full or resumed handshake, through its last flight.</summary>
    public async Task HandshakeAsync()
    {
        List<Tls12OutgoingMessage> flight = await AnswerClientHelloAsync();
        await SendFlightAsync(flight);
        List<Tls12OutgoingMessage> keyExchange = [];
        Tls12OutgoingMessage message;
        while ((message = await ReceiveMessageAsync()).ContentType != TlsContentType.ChangeCipherSpec)
        {
            keyExchange.Add(message);
        }

        server.ReceiveClientKeyExchange(keyExchange);
        reader = Tls12RecordReadState.Create(server.RecordProtection, server.KeyBlock.ClientWrite);
        await SendFlightAsync(server.ReceiveClientFinished([message, await ReceiveMessageAsync()]));
    }

    /// <summary>Reads the ClientHello and returns the server's answer, unsent.</summary>
    public async Task<List<Tls12OutgoingMessage>> AnswerClientHelloAsync() => server.Answer((await ReceiveMessageAsync()).Bytes);

    /// <summary>Sends a flight: runs of handshake messages in records of at most <see cref="HandshakeRecordLength" />, and a ChangeCipherSpec that puts the server's keys in force.</summary>
    public async Task SendFlightAsync(IEnumerable<Tls12OutgoingMessage> flight)
    {
        List<byte> handshake = [];
        foreach (Tls12OutgoingMessage message in flight)
        {
            if (message.ContentType == TlsContentType.Handshake)
            {
                handshake.AddRange(message.Bytes);
                continue;
            }

            await SendHandshakeAsync(handshake);
            await SendAsync(TlsContentType.ChangeCipherSpec, message.Bytes);
            writer = Tls12RecordWriteState.Create(server.RecordProtection, server.KeyBlock.ServerWrite, SystemTlsRandomSource.Instance);
        }

        await SendHandshakeAsync(handshake);
    }

    /// <summary>Protects and sends content under the server's current write state.</summary>
    public Task SendAsync(TlsContentType type, byte[] content) => SendRawAsync(writer.Protect(type, content));

    /// <summary>Protects content without sending it, so a test can change the record before it goes.</summary>
    public byte[] Protect(TlsContentType type, byte[] content) => writer.Protect(type, content);

    public async Task SendRawAsync(byte[] records)
    {
        await transport.WriteAsync(records);
        await transport.FlushAsync();
    }

    /// <summary>Reads the client's next record, unprotected; <see langword="null" /> when the client closed the transport.</summary>
    public async Task<Tls12OutgoingMessage?> ReceiveAsync()
    {
        byte[] header = new byte[5];
        if (await transport.ReadAtLeastAsync(header, 5, false) < 5)
        {
            return null;
        }

        byte[] fragment = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        await transport.ReadExactlyAsync(fragment);
        TlsContentType type = (TlsContentType)header[0];
        RecordVersions.Add(BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1)));
        byte[] content = reader.Unprotect(type, fragment).Value;
        if (type == TlsContentType.ApplicationData)
        {
            ApplicationDataLengths.Add(content.Length);
        }

        return new Tls12OutgoingMessage(type, content);
    }

    /// <summary>Reads application data until <paramref name="length" /> bytes have arrived.</summary>
    public async Task<byte[]> ReceiveApplicationDataAsync(int length)
    {
        List<byte> received = [];
        while (received.Count < length)
        {
            Tls12OutgoingMessage content = await ReceiveMessageAsync();
            Assert.AreEqual(TlsContentType.ApplicationData, content.ContentType);
            received.AddRange(content.Bytes);
        }

        return [.. received];
    }

    private async Task<Tls12OutgoingMessage> ReceiveMessageAsync() => (await ReceiveAsync())!;

    private async Task SendHandshakeAsync(List<byte> handshake)
    {
        for (int offset = 0; offset < handshake.Count; offset += HandshakeRecordLength)
        {
            await SendAsync(TlsContentType.Handshake, [.. handshake.Skip(offset).Take(HandshakeRecordLength)]);
        }

        handshake.Clear();
    }
}
