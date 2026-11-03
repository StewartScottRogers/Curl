using System.Buffers.Binary;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Http3Server;
using Curl.Http2;
using Curl.Http3;
using Curl.Tls;

namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>
/// A WebTransport over HTTP/3 server (draft-ietf-webtrans-http3-16) on one
/// <see cref="Http3ServerConnection" />, written from the specification for the extended conformance
/// cases: it opens its control stream with the SETTINGS its <see cref="WebTransportServerScript" />
/// chooses, answers each extended CONNECT with the scripted status, echoes bidirectional streams,
/// unidirectional streams and datagrams when scripted, and sends WT_CLOSE_SESSION when scripted. It
/// records what the client sent: its SETTINGS, the CONNECT field section, the WebTransport streams
/// and datagrams, the capsules on the CONNECT stream and that stream's FIN.
/// </summary>
/// <param name="tlsSettings">The certificate and key the QUIC handshake presents.</param>
/// <param name="script">What the server does.</param>
/// <param name="random">The randomness of the handshake and the connection id.</param>
public sealed class WebTransportReferenceServer(Http3ServerTlsSettings tlsSettings, WebTransportServerScript script, ITlsRandomSource random) : IDatagramReferenceServer, IDisposable
{
    private readonly Http3ServerConnection connection = new(tlsSettings, random);

    private readonly Lock gate = new();

    private readonly List<WebTransportServerClientStream> streams = [];

    private readonly List<WebTransportRecordedStream> bidirectionalStreams = [];

    private readonly List<WebTransportRecordedStream> unidirectionalStreams = [];

    private readonly List<WebTransportRecordedDatagram> datagrams = [];

    private readonly List<WebTransportRecordedCapsule> capsules = [];

    private readonly QpackDecoder decoder = new(0, 0);

    private readonly QpackEncoder encoder = new(0, 0);

    private bool controlStreamOpened;

    private ulong? sessionStreamId;

    private bool closeSent;

    private int echoes;

    /// <summary>Gets the client's SETTINGS, or null until they arrive.</summary>
    public IReadOnlyList<Http3Setting>? ClientSettings { get; private set; }

    /// <summary>Gets the CONNECT request's field section, decoded with QPACK, or null until it arrives.</summary>
    public IReadOnlyList<HeaderField>? ConnectFields { get; private set; }

    /// <summary>Gets a value indicating whether the client's FIN on the CONNECT stream arrived.</summary>
    public bool IsConnectStreamFinished { get; private set; }

    /// <summary>Gets the WebTransport bidirectional streams the client opened.</summary>
    public IReadOnlyList<WebTransportRecordedStream> BidirectionalStreams => bidirectionalStreams;

    /// <summary>Gets the WebTransport unidirectional streams the client opened.</summary>
    public IReadOnlyList<WebTransportRecordedStream> UnidirectionalStreams => unidirectionalStreams;

    /// <summary>Gets the HTTP datagrams the client sent.</summary>
    public IReadOnlyList<WebTransportRecordedDatagram> Datagrams => datagrams;

    /// <summary>Gets the capsules the client sent on the CONNECT stream.</summary>
    public IReadOnlyList<WebTransportRecordedCapsule> Capsules => capsules;

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
    {
        lock (gate)
        {
            List<byte[]> toSend = [.. connection.Receive(datagram)];
            if (connection.IsHandshakeComplete && !connection.IsClosed)
            {
                Serve();
                toSend.AddRange(connection.TakeDatagramsToSend());
            }

            return [.. toSend.Select(bytes => new ReadOnlyMemory<byte>(bytes))];
        }
    }

    /// <inheritdoc />
    public void Dispose() => connection.Dispose();

    private static byte[] CloseSessionCapsule(WebTransportCloseSession close)
    {
        byte[] value = new byte[4 + Encoding.UTF8.GetByteCount(close.Message)];
        BinaryPrimitives.WriteUInt32BigEndian(value, close.Code);
        Encoding.UTF8.GetBytes(close.Message, value.AsSpan(4));
        List<byte> capsule = [];
        WebTransportServerBuffer.WriteVariableLengthInteger(capsule, WebTransportCodepoints.CloseSessionCapsule);
        WebTransportServerBuffer.WriteVariableLengthInteger(capsule, value.Length);
        capsule.AddRange(value);
        return [.. capsule];
    }

    private static IReadOnlyList<Http3Setting> DecodeSettings(byte[] payload)
    {
        var buffer = new WebTransportServerBuffer();
        buffer.Append(payload);
        List<Http3Setting> settings = [];
        while (buffer.TryReadVariableLengthInteger(out long identifier) && buffer.TryReadVariableLengthInteger(out long value))
        {
            settings.Add(new Http3Setting(identifier, value));
        }

        return settings;
    }

    private void Serve()
    {
        OpenControlStream();
        while (connection.AcceptStream() is { } stream)
        {
            streams.Add(new WebTransportServerClientStream(stream));
        }

        foreach (WebTransportServerClientStream stream in streams)
        {
            ReadStream(stream);
        }

        while (connection.AcceptDatagram() is { } payload)
        {
            ReceiveDatagram(payload);
        }

        CloseSessionWhenDue();
    }

    private void OpenControlStream()
    {
        if (controlStreamOpened)
        {
            return;
        }

        controlStreamOpened = true;
        List<Http3Setting> settings = [];
        settings.AddRange(script.SendEnableConnectProtocol ? [new Http3Setting(Http3SettingIdentifier.EnableConnectProtocol, 1)] : []);
        settings.AddRange(script.SendH3Datagram ? [new Http3Setting(Http3SettingIdentifier.H3Datagram, 1)] : []);
        settings.AddRange(script.MaxSessions is { } maximum ? [new Http3Setting(WebTransportCodepoints.MaxSessionsSetting, maximum)] : []);
        connection.Write(connection.OpenUnidirectionalStream(), [(byte)Http3UnidirectionalStreamType.Control, .. new Http3SettingsFrame(settings).ToBytes()], endStream: false);
    }

    private void ReadStream(WebTransportServerClientStream stream)
    {
        stream.Buffer.Append(stream.Stream.Read());
        if (stream.Kind == WebTransportServerClientStreamKind.Unknown)
        {
            stream.Kind = stream.Stream.IsUnidirectional ? ClassifyUnidirectional(stream) : ClassifyBidirectional(stream);
        }

        switch (stream.Kind)
        {
            case WebTransportServerClientStreamKind.Connect:
                ReadConnectStream(stream);
                break;
            case WebTransportServerClientStreamKind.Control:
                ReadControlStream(stream);
                break;
            case WebTransportServerClientStreamKind.WebTransport:
                ReadWebTransportStream(stream);
                break;
        }
    }

    private WebTransportServerClientStreamKind ClassifyBidirectional(WebTransportServerClientStream stream)
    {
        int mark = stream.Buffer.Position;
        if (!stream.Buffer.TryReadVariableLengthInteger(out long signal))
        {
            return WebTransportServerClientStreamKind.Unknown;
        }

        if (signal != WebTransportCodepoints.BidirectionalStreamSignal)
        {
            stream.Buffer.Rewind(mark);
            return WebTransportServerClientStreamKind.Connect;
        }

        return StartWebTransportStream(stream, mark, bidirectionalStreams, script.EchoBidirectionalStreams);
    }

    private WebTransportServerClientStreamKind ClassifyUnidirectional(WebTransportServerClientStream stream)
    {
        int mark = stream.Buffer.Position;
        if (!stream.Buffer.TryReadVariableLengthInteger(out long type))
        {
            return WebTransportServerClientStreamKind.Unknown;
        }

        return type switch
        {
            (long)Http3UnidirectionalStreamType.Control => WebTransportServerClientStreamKind.Control,
            WebTransportCodepoints.UnidirectionalStreamType => StartWebTransportStream(stream, mark, unidirectionalStreams, script.EchoUnidirectionalStreams),
            _ => WebTransportServerClientStreamKind.Ignored,
        };
    }

    // Reads the session id after the stream's signal or type, records the stream and chooses where
    // its echo goes: the same stream when bidirectional, a new unidirectional stream with the same
    // header otherwise.
    private WebTransportServerClientStreamKind StartWebTransportStream(WebTransportServerClientStream stream, int mark, List<WebTransportRecordedStream> recorded, bool echo)
    {
        if (!stream.Buffer.TryReadVariableLengthInteger(out long sessionId))
        {
            stream.Buffer.Rewind(mark);
            return WebTransportServerClientStreamKind.Unknown;
        }

        stream.Recorded = new WebTransportRecordedStream(stream.Stream.Id, sessionId);
        recorded.Add(stream.Recorded);
        stream.EchoStreamId = echo ? OpenEchoStream(stream.Stream, sessionId) : null;
        return WebTransportServerClientStreamKind.WebTransport;
    }

    private ulong OpenEchoStream(Http3ServerStream stream, long sessionId)
    {
        if (!stream.IsUnidirectional)
        {
            return stream.Id;
        }

        ulong id = connection.OpenUnidirectionalStream();
        List<byte> header = [];
        WebTransportServerBuffer.WriteVariableLengthInteger(header, WebTransportCodepoints.UnidirectionalStreamType);
        WebTransportServerBuffer.WriteVariableLengthInteger(header, sessionId);
        connection.Write(id, [.. header], endStream: false);
        return id;
    }

    private void ReadWebTransportStream(WebTransportServerClientStream stream)
    {
        byte[] payload = stream.Buffer.TakeRest();
        stream.Recorded!.Append(payload);
        bool fin = stream.Stream.IsReadComplete && !stream.Recorded.IsFinished;
        if (fin)
        {
            stream.Recorded.Finish();
        }

        if (stream.EchoStreamId is { } echoStreamId && (payload.Length > 0 || fin))
        {
            connection.Write(echoStreamId, payload, fin);
            echoes += fin ? 1 : 0;
        }
    }

    private void ReadControlStream(WebTransportServerClientStream stream)
    {
        while (stream.Buffer.TryReadTypeLengthValue(out long type, out byte[] payload))
        {
            ClientSettings ??= type == (long)Http3FrameType.Settings ? DecodeSettings(payload) : null;
        }
    }

    private void ReadConnectStream(WebTransportServerClientStream stream)
    {
        while (stream.Buffer.TryReadTypeLengthValue(out long type, out byte[] payload))
        {
            ReadConnectFrame(stream, type, payload);
        }

        if (stream.Stream.IsReadComplete && !stream.IsFinRead)
        {
            stream.IsFinRead = true;
            IsConnectStreamFinished = true;
        }
    }

    // The first HEADERS frame is the CONNECT request; DATA frames carry the capsules; any other
    // frame, trailers among them, is read and ignored.
    private void ReadConnectFrame(WebTransportServerClientStream stream, long type, byte[] payload)
    {
        if (type == (long)Http3FrameType.Headers && !stream.IsHeadersRead)
        {
            stream.IsHeadersRead = true;
            AnswerConnect(stream.Stream.Id, payload);
        }
        else if (type == (long)Http3FrameType.Data)
        {
            stream.Capsules.Append(payload);
            ReadCapsules(stream.Capsules);
        }
    }

    // With no dynamic table a field section never blocks, so a section that fails to decode throws.
    private void AnswerConnect(ulong streamId, byte[] encodedFieldSection)
    {
        decoder.TryDecodeFieldSection((long)streamId, encodedFieldSection, out IReadOnlyList<HeaderField>? fields);
        ConnectFields = fields;
        bool accepted = script.ConnectStatus.StartsWith('2');
        sessionStreamId = accepted ? streamId : null;
        connection.Write(streamId, new Http3HeadersFrame(encoder.EncodeFieldSection((long)streamId, [new HeaderField(":status", script.ConnectStatus)])).ToBytes(), endStream: !accepted);
    }

    private void ReadCapsules(WebTransportServerBuffer buffer)
    {
        while (buffer.TryReadTypeLengthValue(out long type, out byte[] value))
        {
            capsules.Add(new WebTransportRecordedCapsule(type, value));
        }
    }

    private void ReceiveDatagram(byte[] datagram)
    {
        var buffer = new WebTransportServerBuffer();
        buffer.Append(datagram);
        buffer.TryReadVariableLengthInteger(out long quarterStreamId);
        datagrams.Add(new WebTransportRecordedDatagram(quarterStreamId, buffer.TakeRest()));
        if (script.EchoDatagrams)
        {
            connection.SendDatagram(datagram);
            echoes++;
        }
    }

    private void CloseSessionWhenDue()
    {
        if (script.CloseSession is { } close && sessionStreamId is { } streamId && !closeSent && echoes >= script.CloseSessionAfterEchoes)
        {
            closeSent = true;
            connection.Write(streamId, new Http3DataFrame(CloseSessionCapsule(close)).ToBytes(), endStream: true);
        }
    }
}
