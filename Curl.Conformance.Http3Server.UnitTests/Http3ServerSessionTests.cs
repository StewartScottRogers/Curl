using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerSessionTests
{
    private const string Reply2500 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    private static readonly ConnectTarget Target = new("localhost", 9010, UseTls: false);

    [TestMethod]
    public async Task ReceiveAsync_GetRequest_RelaysItAsNghttpxH3DoesAndAnswersOnTheStream()
    {
        var backend = new RecordingBackendConnector(Reply2500);
        using var session = new Http3ServerSession(new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance), backend, Target);
        using var client = await ConnectAsync(session);
        client.Streams.OpenUnidirectional()!.Write([0x00, 0x04, 0x00], endStream: false);
        var stream = client.Streams.OpenBidirectional()!;
        stream.Write(Http3TestClient.Headers((":method", "GET"), (":scheme", "https"), (":authority", "localhost:9010"), (":path", "/2500"), ("user-agent", "curl/8.22.0"), ("accept", "*/*")), endStream: false);

        await Http3TestClient.PumpAsync(client, session, client.TakeDatagramsToSend());
        var beforeFin = backend.Requests.Count;
        stream.Write([], endStream: true);
        await Http3TestClient.PumpAsync(client, session, client.TakeDatagramsToSend());
        (IReadOnlyList<HeaderField> fields, byte[] body) = await Http3TestClient.ReadReplyAsync(Http3TestClient.ReadAll(stream));

        Assert.AreEqual(0, beforeFin);
        Assert.AreEqual("GET https://localhost:9010/2500 HTTP/1.1\r\nHost: localhost:9010\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nVia: 3 nghttpx\r\n\r\n", backend.Requests.Single());
        Assert.IsTrue(stream.IsReadComplete);
        Assert.AreEqual(":status: 200|date: Tue, 09 Nov 2010 14:49:00 GMT|content-length: 6|content-type: text/html|funny-head: yesyes|server: nghttpx|via: 1.1 nghttpx", string.Join("|", fields.Select(f => $"{f.Name}: {f.Value}")));
        Assert.AreEqual("-foo-\n", Encoding.ASCII.GetString(body));
        var control = client.Streams.AcceptUnidirectional()!;
        Assert.AreEqual(3UL, control.Id);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x04, 0x00 }, Http3TestClient.ReadAll(control));
    }

    [TestMethod]
    public async Task ReceiveAsync_PostWithoutABodyInTheReply_SendsHeadersAndTheFinOnly()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 201 OK\r\nContent-Length: 0\r\n\r\n");
        using var session = new Http3ServerSession(new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance), backend, Target);
        using var client = await ConnectAsync(session);
        var stream = client.Streams.OpenBidirectional()!;
        stream.Write([.. Http3TestClient.Headers((":method", "POST"), (":scheme", "https"), (":authority", "127.0.0.1:9010"), (":path", "/2501"), ("content-length", "3")), .. new Curl.Http3.Http3DataFrame("moo"u8.ToArray()).ToBytes()], endStream: true);

        await Http3TestClient.PumpAsync(client, session, client.TakeDatagramsToSend());
        (IReadOnlyList<HeaderField> fields, byte[] body) = await Http3TestClient.ReadReplyAsync(Http3TestClient.ReadAll(stream));

        Assert.AreEqual("POST https://127.0.0.1:9010/2501 HTTP/1.1\r\nHost: 127.0.0.1:9010\r\nContent-Length: 3\r\nVia: 3 nghttpx\r\n\r\nmoo", backend.Requests.Single());
        Assert.AreEqual("201", fields[0].Value);
        Assert.IsEmpty(body);
        Assert.IsTrue(stream.IsReadComplete);
    }

    [TestMethod]
    public async Task ReceiveAsync_RequestStreamsWithoutHeaders_CloseTheConnectionWithMessageError()
    {
        var backend = new RecordingBackendConnector(Reply2500);
        using var session = new Http3ServerSession(new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance), backend, Target);
        using var client = await ConnectAsync(session);
        client.Streams.OpenBidirectional()!.Write(new Curl.Http3.Http3DataFrame("x"u8.ToArray()).ToBytes(), endStream: true);
        client.Streams.OpenBidirectional()!.Write(new Curl.Http3.Http3DataFrame("y"u8.ToArray()).ToBytes(), endStream: true);

        var answers = new List<byte[]>();
        foreach (var datagram in client.TakeDatagramsToSend())
        {
            answers.AddRange(await session.ReceiveAsync(datagram, CancellationToken.None));
        }

        foreach (var datagram in answers)
        {
            client.Receive(datagram);
        }

        Assert.IsTrue(session.Connection.IsClosed);
        Assert.AreEqual(Http3ServerSession.MessageError, client.Failure!.ServerClose!.ErrorCode);
        Assert.IsEmpty(backend.Requests);
        Assert.IsEmpty(await session.ReceiveAsync(new byte[1200], CancellationToken.None));
    }

    [TestMethod]
    public async Task ReceiveAsync_RequestStreamThatBreaksFraming_ClosesTheConnection()
    {
        var backend = new RecordingBackendConnector(Reply2500);
        using var session = new Http3ServerSession(new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance), backend, Target);
        using var client = await ConnectAsync(session);

        // Frame type 0x02 is reserved for HTTP/2's PRIORITY (RFC 9114 section 7.2.8).
        client.Streams.OpenBidirectional()!.Write([0x02, 0x00], endStream: true);
        foreach (var datagram in client.TakeDatagramsToSend())
        {
            await session.ReceiveAsync(datagram, CancellationToken.None);
        }

        Assert.IsTrue(session.Connection.IsClosed);
        Assert.IsEmpty(backend.Requests);
    }

    [TestMethod]
    public async Task ReceiveAsync_BackendRefuses_Answers502()
    {
        using var session = new Http3ServerSession(new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance), new RecordingBackendConnector(null), Target);
        using var client = await ConnectAsync(session);
        var stream = client.Streams.OpenBidirectional()!;
        stream.Write(Http3TestClient.Headers((":method", "GET"), (":scheme", "https"), (":authority", "localhost:9010"), (":path", "/1")), endStream: true);

        await Http3TestClient.PumpAsync(client, session, client.TakeDatagramsToSend());
        (IReadOnlyList<HeaderField> fields, _) = await Http3TestClient.ReadReplyAsync(Http3TestClient.ReadAll(stream));

        Assert.AreEqual("502", fields[0].Value);
    }

    private static async Task<QuicClientConnectionState> ConnectAsync(Http3ServerSession session)
    {
        var client = Http3TestClient.Create();
        await Http3TestClient.PumpAsync(client, session, client.Start());
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }
}
