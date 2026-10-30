using System.Text;
using Curl.Core;
using Curl.Extension.WebTransport.Session;
using Curl.Extension.WebTransport.Wire;
using Curl.Http3;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport;

/// <summary>
/// Checks <see cref="WebTransportProtocolHandler" /> and its <see cref="WebTransportSession" /> against
/// draft-ietf-webtrans-http3-16 and RFC 9297 over an in-memory HTTP/3 connection.
/// </summary>
[TestClass]
public sealed class WebTransportProtocolHandlerTests
{
    private const int HangTimeoutMilliseconds = 60_000;

    [TestMethod]
    public void SupportedSchemes_IsWebTransport() =>
        CollectionAssert.AreEqual(new[] { "webtransport" }, new WebTransportProtocolHandler(null!, null!).SupportedSchemes.ToArray());

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new WebTransportProtocolHandler(null!, null!).ExecuteAsync(null!));

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section3_1_SendsH3DatagramAndWtEnabledSettings()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = Echo };

        await RunAsync(connection, "webtransport://example.com/echo", "hi");

        byte[] control = connection.UnidirectionalStreams[0].Written.ToArray();
        Assert.AreEqual(0x00, control[0], "control stream type");
        CollectionAssert.IsSubsetOf(Varints(0x33, 1), control.ToList(), "SETTINGS_H3_DATAGRAM 1");
        CollectionAssert.IsSubsetOf(Varints(WebTransportWire.EnabledSetting, 1), control.ToList(), "SETTINGS_WT_ENABLED 1");
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section3_2_OpensTheSessionWithExtendedConnect()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = Echo };

        await RunAsync(connection, "webtransport://example.com:4433/chat?room=1&wt=bidi", "hi");

        InMemoryMultiplexedStream connect = connection.BidirectionalStreams[0];
        Assert.AreEqual(0, connect.StreamId);
        Assert.IsTrue(connect.IsWriteEnded, "the session stream is ended after WT_CLOSE_SESSION");
        List<HeaderFieldText> fields = DecodeRequest(connect.Written.ToArray());
        CollectionAssert.AreEqual(
            new[] { new HeaderFieldText(":method", "CONNECT"), new(":protocol", "webtransport-h3"), new(":scheme", "https"), new(":authority", "example.com:4433"), new(":path", "/chat?room=1") },
            fields);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section4_3_SendsTheDataOnABidirectionalStreamAndWritesTheAnswer()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = Echo };

        (TransferResult result, string output) = await RunAsync(connection, "webtransport://example.com/echo", "hello");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", output);
        Assert.AreEqual(5, result.BytesTransferred);
        CollectionAssert.AreEqual((byte[])[0x40, 0x41, 0x00, .. "hello"u8.ToArray()], connection.BidirectionalStreams[1].Written.ToArray());
        Assert.AreEqual((long)Http3ErrorCode.NoError, connection.ClosedWith);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section6_ClosesTheSessionWithWtCloseSessionInAData()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = Echo };

        await RunAsync(connection, "webtransport://example.com/echo", "x");

        byte[] written = connection.BidirectionalStreams[0].Written.ToArray();
        byte[] closing = [0x00, 0x07, 0x68, 0x43, 0x04, 0x00, 0x00, 0x00, 0x00];
        CollectionAssert.AreEqual(closing, written[^closing.Length..], "DATA frame holding WT_CLOSE_SESSION (0x2843), error 0, no message");
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section4_2_SendsOnAUnidirectionalStreamAndWritesTheServersStreamOfTheSession()
    {
        ScriptedHttp3Connection connection = new()
        {
            AnswerStream = (server, _) =>
            {
                server.AddPeerStream([0x21, .. "grease"u8.ToArray()]);
                server.AddPeerStream([0x40, 0x54, 0x04, .. "other"u8.ToArray()]);
                server.AddPeerStream([0x40, 0x54, 0x00, .. "mine"u8.ToArray()]);
            },
        };

        (TransferResult result, string output) = await RunAsync(connection, "webtransport://example.com/u?wt=uni", "up");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("mine", output);
        CollectionAssert.AreEqual((byte[])[0x40, 0x54, 0x00, .. "up"u8.ToArray()], connection.UnidirectionalStreams[3].Written.ToArray());
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section4_2_StopsAServerStreamOfAnotherSessionWithBufferedStreamRejected()
    {
        InMemoryMultiplexedStream? other = null;
        ScriptedHttp3Connection connection = new()
        {
            AnswerStream = (server, _) =>
            {
                other = server.AddPeerStream(0x40, 0x54, 0x08);
                server.AddPeerStream(0x40, 0x54, 0x00);
            },
        };

        await RunAsync(connection, "webtransport://example.com/u?wt=uni", null);

        Assert.AreEqual(WebTransportWire.BufferedStreamRejected, other!.AbortedWith);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_ServerStreamEndsInsideItsHeader_IsARecvError()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = (server, _) => server.AddPeerStream(0x40, 0x54) };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/u?wt=uni", "x");

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("A server stream ended inside its WebTransport header", result.ErrorMessage);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Rfc9297Section2_1_SendsADatagramWithTheQuarterStreamIdAndWritesTheSessionsAnswer()
    {
        ScriptedHttp3Connection connection = new() { DatagramAnswers = [Http3Datagram.Encode(4, "other"u8), Http3Datagram.Encode(0, "pong"u8)] };

        (TransferResult result, string output) = await RunAsync(connection, "webtransport://example.com/d?wt=datagram", "ping");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("pong", output);
        CollectionAssert.AreEqual((byte[])[0x00, .. "ping"u8.ToArray()], connection.SentDatagrams.Single());
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_DatagramLongerThanThePeerAccepts_IsASendError()
    {
        ScriptedHttp3Connection connection = new() { MaxDatagramPayloadLength = 4 };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/d?wt=datagram", "ping");

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("The data does not fit in one datagram: at most 4 bytes, quarter stream ID included", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section3_1_ConnectionWithoutQuicDatagrams_IsAWeirdServerReply(int maxPayload)
    {
        IMultiplexedConnection connection = maxPayload < 0 ? new StreamsOnlyConnection(new ScriptedHttp3Connection()) : new ScriptedHttp3Connection { MaxDatagramPayloadLength = maxPayload };

        TransferResult result = await new WebTransportProtocolHandler((_, _) => ValueTask.FromResult(connection)).ExecuteAsync(Context("webtransport://example.com/d?wt=datagram", "x", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The server does not accept QUIC datagrams", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(WebTransportWire.EnabledSetting)]
    [DataRow(Http3SettingIdentifier.EnableConnectProtocol)]
    [DataRow(Http3SettingIdentifier.H3Datagram)]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section3_1_ServerSettingsWithoutOneRequiredSetting_IsAWeirdServerReplyAndSendsNoConnect(long missing)
    {
        ScriptedHttp3Connection connection = new() { ServerSettings = [.. new ScriptedHttp3Connection().ServerSettings!.Where(setting => setting.Identifier != missing)] };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/", "x");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The server does not offer WebTransport over HTTP/3", result.ErrorMessage);
        Assert.AreEqual(0, connection.BidirectionalStreams[0].Written.Length);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_ServerStreamsEndBeforeSettings_IsARecvError()
    {
        ScriptedHttp3Connection connection = new() { ServerSettings = null };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/", "x");

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("The HTTP/3 connection failed before the server's SETTINGS", result.ErrorMessage);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_Section3_2_NonSuccessStatus_IsAnHttpReturnedError()
    {
        ScriptedHttp3Connection connection = new() { ConnectAnswer = ScriptedHttp3Connection.Headers("404") };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/", "x");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The server refused the WebTransport session: HTTP status 404", result.ErrorMessage);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_FramesBeforeTheResponseHeaders_AreSkipped()
    {
        ScriptedHttp3Connection connection = new() { ConnectAnswer = [0x00, 0x01, 0x78, .. ScriptedHttp3Connection.Headers("200")], AnswerStream = Echo };

        (TransferResult result, string output) = await RunAsync(connection, "webtransport://example.com/", "ok");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", output);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_SessionStreamEndsBeforeTheResponse_IsARecvError()
    {
        ScriptedHttp3Connection connection = new() { ConnectAnswer = [] };

        Task<(TransferResult, string)> running = RunAsync(connection, "webtransport://example.com/", "x");
        while (connection.BidirectionalStreams.Count == 0)
        {
            await Task.Yield();
        }

        connection.BidirectionalStreams[0].EndReceiving();
        (TransferResult result, _) = await running;

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("The server ended the session stream before answering the CONNECT", result.ErrorMessage);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_StreamFailure_IsARecvError()
    {
        ScriptedHttp3Connection connection = new() { DataStreamOpenFailure = new IOException("stream limit") };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/", "x");

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("WebTransport session failed: stream limit", result.ErrorMessage);
    }

    [TestMethod]
    [Timeout(HangTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ExecuteAsync_MalformedDatagram_IsARecvError()
    {
        ScriptedHttp3Connection connection = new() { DatagramAnswers = [[]] };

        (TransferResult result, _) = await RunAsync(connection, "webtransport://example.com/?wt=datagram", "x");

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "WebTransport session failed: ");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownMode_IsAUrlMalformatBeforeDialling()
    {
        bool dialled = false;
        WebTransportProtocolHandler handler = new((_, _) =>
        {
            dialled = true;
            return ValueTask.FromResult<IMultiplexedConnection>(new ScriptedHttp3Connection());
        });

        TransferResult result = await handler.ExecuteAsync(Context("webtransport://example.com/?wt=x", null, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("Unknown WebTransport mode \"wt=x\": use wt=bidi, wt=uni or wt=datagram", result.ErrorMessage);
        Assert.IsFalse(dialled);
    }

    [TestMethod]
    public async Task ExecuteAsync_DialFailsWithAnIOException_IsARecvError()
    {
        WebTransportProtocolHandler handler = new((_, _) => throw new IOException("network down"));

        TransferResult result = await handler.ExecuteAsync(Context("webtransport://example.com/echo", "x", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("WebTransport session failed: network down", result.ErrorMessage);
    }

    private static void Echo(ScriptedHttp3Connection server, InMemoryMultiplexedStream stream)
    {
        byte[] written = stream.Written.ToArray();
        stream.Receive(written[3..]);
        stream.EndReceiving();
    }

    private static async Task<(TransferResult Result, string Output)> RunAsync(IMultiplexedConnection connection, string url, string? data)
    {
        MemoryStream output = new();
        TransferResult result = await new WebTransportProtocolHandler((_, _) => ValueTask.FromResult(connection)).ExecuteAsync(Context(url, data, output));
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }

    private static TransferContext Context(string url, string? data, MemoryStream output) => new()
    {
        Url = CurlUrl.Parse(url),
        PostData = data is null ? null : Encoding.UTF8.GetBytes(data),
        Output = output,
    };

    private static List<byte> Varints(long identifier, long value)
    {
        List<byte> bytes = [.. new byte[HttpDatagramVariableLengthInteger.GetEncodedLength((ulong)identifier) + HttpDatagramVariableLengthInteger.GetEncodedLength((ulong)value)]];
        byte[] buffer = [.. bytes];
        int length = HttpDatagramVariableLengthInteger.Write((ulong)identifier, buffer);
        HttpDatagramVariableLengthInteger.Write((ulong)value, buffer.AsSpan(length));
        return [.. buffer];
    }

    private static List<HeaderFieldText> DecodeRequest(byte[] written)
    {
        int position = 0;
        HttpDatagramVariableLengthInteger.TryRead(written, out ulong type, out int consumed);
        position += consumed;
        HttpDatagramVariableLengthInteger.TryRead(written.AsSpan(position), out ulong length, out consumed);
        position += consumed;
        Assert.AreEqual(1UL, type, "HEADERS");
        Assert.IsTrue(new QpackDecoder(0, 0).TryDecodeFieldSection(0, written.AsSpan(position, (int)length), out IReadOnlyList<Curl.Http2.HeaderField>? fields));
        return [.. fields.Select(field => new HeaderFieldText(field.Name, field.Value))];
    }

    private sealed record HeaderFieldText(string Name, string Value);

    // A connection that carries streams but no datagrams at all.
    private sealed class StreamsOnlyConnection(ScriptedHttp3Connection inner) : IMultiplexedConnection
    {
        public System.Net.EndPoint? RemoteEndPoint => null;

        public System.Net.EndPoint? LocalEndPoint => null;

        public string ApplicationProtocol => "h3";

        public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => inner.OpenBidirectionalStreamAsync(cancellationToken);

        public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => inner.OpenUnidirectionalStreamAsync(cancellationToken);

        public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => inner.AcceptUnidirectionalStreamAsync(cancellationToken);

        public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => inner.CloseAsync(applicationErrorCode, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
