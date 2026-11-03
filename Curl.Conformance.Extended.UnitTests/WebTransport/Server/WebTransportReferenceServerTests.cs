using System.Text;
using Curl.Conformance.Extended.WebTransport.Server;
using Curl.Http3;
using Curl.Quic;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Server;

[TestClass]
public sealed class WebTransportReferenceServerTests
{
    [TestMethod]
    public void Answer_DefaultScript_SendsTheThreeWebTransportSettings()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);

        CollectionAssert.AreEqual(new long[] { 0x08, 1, 0x33, 1, WebTransportCodepoints.MaxSessionsSetting, 1 }, ServerSettings(client));
    }

    [TestMethod]
    public void Answer_EverySettingLeftOut_SendsAnEmptySettingsFrame()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { SendEnableConnectProtocol = false, SendH3Datagram = false, MaxSessions = null });
        using var client = WebTransportTestClient.Connect(server);

        Assert.IsEmpty(ServerSettings(client));
    }

    [TestMethod]
    public void Answer_ClientControlAndQpackStreams_RecordsTheClientSettingsAndIgnoresTheRest()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);

        client.Streams.OpenUnidirectional()!.Write([0x00, 0x21, 0x00, 0x04, 0x05, 0x08, 0x01, 0x33, 0x01, 0x07], endStream: false);
        client.Streams.OpenUnidirectional()!.Write([0x02, 0x3f], endStream: false);
        WebTransportTestClient.Flush(client, server);

        Assert.HasCount(2, server.ClientSettings!);
        Assert.AreEqual(new Http3Setting(0x08, 1), server.ClientSettings![0]);
        Assert.AreEqual(new Http3Setting(0x33, 1), server.ClientSettings[1]);
        Assert.IsEmpty(server.UnidirectionalStreams);
    }

    [TestMethod]
    public void Answer_ClientSettingsWholePairs_RecordsEveryPair()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);

        client.Streams.OpenUnidirectional()!.Write([0x00, 0x04, 0x02, 0x33, 0x01], endStream: false);
        WebTransportTestClient.Flush(client, server);

        Assert.AreEqual(new Http3Setting(0x33, 1), server.ClientSettings!.Single());
    }

    [TestMethod]
    public void Answer_ExtendedConnect_RecordsTheFieldsAndAnswersTheScriptedStatusOnAnOpenStream()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);

        QuicStream connect = WebTransportTestClient.OpenSession(client, server);

        Assert.AreEqual(":protocol", server.ConnectFields![1].Name);
        Assert.AreEqual("webtransport", server.ConnectFields[1].Value);
        Assert.AreEqual("200", Status(WebTransportTestClient.Read(connect)));
        Assert.IsFalse(connect.IsReadComplete);
        Assert.IsFalse(server.IsConnectStreamFinished);
    }

    [TestMethod]
    public void Answer_ConnectRefused_EndsTheStreamAndSendsNoCloseSession()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { ConnectStatus = "404", CloseSession = new WebTransportCloseSession(1, "x") });
        using var client = WebTransportTestClient.Connect(server);

        QuicStream connect = WebTransportTestClient.OpenSession(client, server);

        List<(long Type, byte[] Payload)> frames = WebTransportTestClient.Frames(WebTransportTestClient.Read(connect));
        Assert.HasCount(1, frames);
        Assert.IsTrue(connect.IsReadComplete);
    }

    [TestMethod]
    public void Answer_CapsulesTrailersAndFinOnTheConnectStream_RecordsThem()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);
        QuicStream connect = WebTransportTestClient.OpenSession(client, server);

        connect.Write([.. new Http3DataFrame(new byte[] { 0x80, 0x00, 0x78, 0xae, 0x00, 0x40 }).ToBytes()], endStream: false);
        WebTransportTestClient.Flush(client, server);
        connect.Write([.. new Http3DataFrame(new byte[] { 0x00 }).ToBytes(), .. WebTransportTestClient.ConnectHeaders()], endStream: true);
        WebTransportTestClient.Flush(client, server);

        Assert.HasCount(1, server.Capsules);
        Assert.AreEqual(0x78ae, server.Capsules[0].Type);
        Assert.IsEmpty(server.Capsules[0].Value);
        Assert.IsTrue(server.IsConnectStreamFinished);
        Assert.AreEqual("CONNECT", server.ConnectFields![0].Value);
    }

    [TestMethod]
    public void Answer_BidirectionalStreamInPiecesWithEcho_EchoesThePayloadAndFin()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { EchoBidirectionalStreams = true });
        using var client = WebTransportTestClient.Connect(server);
        WebTransportTestClient.OpenSession(client, server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        foreach (byte[] piece in new byte[][] { [0x40], [0x41], [0x00], [(byte)'a', (byte)'b'] })
        {
            stream.Write(piece, endStream: false);
            WebTransportTestClient.Flush(client, server);
        }

        stream.Write("c"u8, endStream: true);
        WebTransportTestClient.Flush(client, server);

        WebTransportRecordedStream recorded = server.BidirectionalStreams.Single();
        Assert.AreEqual(stream.Id, recorded.StreamId);
        Assert.AreEqual(0, recorded.SessionId);
        CollectionAssert.AreEqual("abc"u8.ToArray(), recorded.Bytes);
        Assert.IsTrue(recorded.IsFinished);
        CollectionAssert.AreEqual("abc"u8.ToArray(), WebTransportTestClient.Read(stream));
        Assert.IsTrue(stream.IsReadComplete);
    }

    [TestMethod]
    public void Answer_BidirectionalStreamWithoutEcho_RecordsItAndSendsNothingBack()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        stream.Write([0x40, 0x41, 0x00, .. "hi"u8], endStream: true);
        WebTransportTestClient.Flush(client, server);

        CollectionAssert.AreEqual("hi"u8.ToArray(), server.BidirectionalStreams.Single().Bytes);
        Assert.IsEmpty(WebTransportTestClient.Read(stream));
    }

    [TestMethod]
    public void Answer_UnidirectionalStreamInPiecesWithEcho_EchoesOnAServerStreamWithTheSameHeader()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { EchoUnidirectionalStreams = true });
        using var client = WebTransportTestClient.Connect(server);
        client.Streams.AcceptUnidirectional();
        QuicStream stream = client.Streams.OpenUnidirectional()!;

        stream.Write([0x40], endStream: false);
        WebTransportTestClient.Flush(client, server);
        stream.Write([0x54], endStream: false);
        WebTransportTestClient.Flush(client, server);
        stream.Write([0x00, .. "xyz"u8], endStream: true);
        WebTransportTestClient.Flush(client, server);

        QuicStream echo = client.Streams.AcceptUnidirectional()!;
        CollectionAssert.AreEqual((byte[])[0x40, 0x54, 0x00, .. "xyz"u8.ToArray()], WebTransportTestClient.Read(echo));
        Assert.IsTrue(echo.IsReadComplete);
        Assert.IsTrue(server.UnidirectionalStreams.Single().IsFinished);
    }

    [TestMethod]
    public void Answer_UnidirectionalStreamWithoutEcho_RecordsItOnly()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);
        client.Streams.AcceptUnidirectional();

        client.Streams.OpenUnidirectional()!.Write([0x40, 0x54, 0x04, .. "q"u8], endStream: true);
        WebTransportTestClient.Flush(client, server);

        Assert.AreEqual(4, server.UnidirectionalStreams.Single().SessionId);
        Assert.IsNull(client.Streams.AcceptUnidirectional());
    }

    [TestMethod]
    public void Answer_DatagramWithEcho_RecordsItAndSendsItBack()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { EchoDatagrams = true });
        using var client = WebTransportTestClient.Connect(server);

        client.SendDatagram(new byte[] { 0x00, 1, 2, 3 });
        WebTransportTestClient.Flush(client, server);

        Assert.AreEqual(0, server.Datagrams.Single().QuarterStreamId);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, server.Datagrams.Single().Payload);
        CollectionAssert.AreEqual(new byte[] { 0x00, 1, 2, 3 }, client.TakeReceivedDatagram()!.Value.ToArray());
    }

    [TestMethod]
    public void Answer_DatagramWithoutEcho_RecordsItOnly()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);

        client.SendDatagram(new byte[] { 0x01, 9 });
        WebTransportTestClient.Flush(client, server);

        Assert.AreEqual(1, server.Datagrams.Single().QuarterStreamId);
        Assert.IsNull(client.TakeReceivedDatagram());
    }

    [TestMethod]
    public void Answer_CloseSessionAfterOneEcho_SendsTheCapsuleAndFinOnlyAfterTheEcho()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript { EchoDatagrams = true, CloseSession = new WebTransportCloseSession(7, "bye"), CloseSessionAfterEchoes = 1 });
        using var client = WebTransportTestClient.Connect(server);
        QuicStream connect = WebTransportTestClient.OpenSession(client, server);
        WebTransportTestClient.Read(connect);
        Assert.IsFalse(connect.IsReadComplete);

        client.SendDatagram(new byte[] { 0x00, 5 });
        WebTransportTestClient.Flush(client, server);
        client.SendDatagram(new byte[] { 0x00, 6 });
        WebTransportTestClient.Flush(client, server);

        (long type, byte[] payload) = WebTransportTestClient.Frames(WebTransportTestClient.Read(connect)).Single();
        Assert.AreEqual((long)Http3FrameType.Data, type);
        CollectionAssert.AreEqual((byte[])[0x68, 0x43, 0x07, 0, 0, 0, 7, .. Encoding.UTF8.GetBytes("bye")], payload);
        Assert.IsTrue(connect.IsReadComplete);
    }

    [TestMethod]
    public void Answer_AfterTheClientClosesTheConnection_ServesNothing()
    {
        using var server = WebTransportTestClient.CreateServer(new WebTransportServerScript());
        using var client = WebTransportTestClient.Connect(server);
        IReadOnlyList<byte[]> close = client.CloseWithApplicationError(0);

        Assert.IsEmpty(close.SelectMany(datagram => server.Answer(datagram)));
    }

    private static string Status(byte[] bytes)
    {
        (long _, byte[] payload) = WebTransportTestClient.Frames(bytes).Single();
        new QpackDecoder(0, 0).TryDecodeFieldSection(0, payload, out IReadOnlyList<Curl.Http2.HeaderField>? fields);
        return fields!.Single().Value;
    }

    private static long[] ServerSettings(QuicClientConnectionState client)
    {
        byte[] control = WebTransportTestClient.Read(client.Streams.AcceptUnidirectional()!);
        Assert.AreEqual(0x00, control[0]);
        (long type, byte[] payload) = WebTransportTestClient.Frames(control[1..]).Single();
        Assert.AreEqual((long)Http3FrameType.Settings, type);
        var buffer = new WebTransportServerBuffer();
        buffer.Append(payload);
        List<long> values = [];
        while (buffer.TryReadVariableLengthInteger(out long value))
        {
            values.Add(value);
        }

        return [.. values];
    }
}
