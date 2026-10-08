using Curl.Testing;
using static Curl.Tls.Tls12PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Drives a connected <see cref="Tls12ClientStream" /> against the in-memory TLS 1.2 server
/// over a pipe: the end of the stream with and without <c>close_notify</c>, HelloRequest,
/// and every record the client refuses, each answered with its alert.
/// </summary>
[TestClass]
public sealed class Tls12ClientStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task TransportEndingWithoutCloseNotifyEndsTheStreamWithoutTheCloseNotifyFlag()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, Stream serverEnd) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        await server.SendAsync(TlsContentType.ApplicationData, "last"u8.ToArray());
        await serverEnd.DisposeAsync();
        Diagnostics.Arrange("server sends", "application data \"last\", then closes the transport without close_notify");

        byte[] last = new byte[4];
        await stream.ReadExactlyAsync(last);
        int end = await stream.ReadAsync(new byte[10]);
        int again = await stream.ReadAsync(new byte[10]);
        Diagnostics.Act("reads", $"data {Convert.ToHexString(last)}, then {end} bytes, then {again} bytes; close_notify received {stream.CloseNotifyReceived}");

        Diagnostics.Diff("data", "last"u8, last);
        Diagnostics.Assert("close_notify received", false, stream.CloseNotifyReceived);
        CollectionAssert.AreEqual("last"u8.ToArray(), last);
        Assert.AreEqual(0, end);
        Assert.AreEqual(0, again);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task TransportEndingInsideARecordEndsTheStream()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, Stream serverEnd) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        await server.SendRawAsync(server.Protect(TlsContentType.ApplicationData, "cut"u8.ToArray())[..10]);
        await serverEnd.DisposeAsync();
        Diagnostics.Arrange("server sends", "the first 10 bytes of a protected record, then closes the transport");

        int end = await stream.ReadAsync(new byte[10]);
        Diagnostics.Act("read", $"{end} bytes; close_notify received {stream.CloseNotifyReceived}");

        Diagnostics.Assert("bytes read", 0, end);
        Assert.AreEqual(0, end);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task HelloRequestsWholeOrSplitAcrossRecordsAreIgnored()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        Diagnostics.Arrange("server sends", "a HelloRequest and a half, its other half, then application data \"x\"");

        await server.SendAsync(TlsContentType.Handshake, [0, 0, 0, 0, 0, 0]);
        await server.SendAsync(TlsContentType.Handshake, [0, 0]);
        await server.SendAsync(TlsContentType.ApplicationData, "x"u8.ToArray());
        byte[] data = new byte[1];
        await stream.ReadExactlyAsync(data);
        Diagnostics.Act("read", Convert.ToHexString(data));

        Diagnostics.Diff("data", "x"u8, data);
        CollectionAssert.AreEqual("x"u8.ToArray(), data);
    }

    [TestMethod]
    public async Task CorruptedRecordFailsTheStreamWithBadRecordMac()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        byte[] record = server.Protect(TlsContentType.ApplicationData, "data"u8.ToArray());
        record[^1] ^= 0x01;
        await server.SendRawAsync(record);
        Diagnostics.Arrange("server sends", "an application data record with its last byte flipped");

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription sent = await ReceiveFatalAlertAsync(server);
        TlsAlertException readAgain = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException write = await Assert.ThrowsExactlyAsync<TlsAlertException>(async () => await stream.WriteAsync(new byte[1]));
        Diagnostics.Act("failure", $"{failure.Alert}, from server {failure.IsFromServer}; alert sent {sent}; read again {readAgain.Alert}; write {write.Alert}");

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, sent);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, readAgain.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, write.Alert);
    }

    [TestMethod]
    public async Task FatalAlertFromTheServerFailsTheStreamWithoutAnAnswer()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        await server.SendAsync(TlsContentType.Alert, [2, 80]);
        Diagnostics.Arrange("server sends", "fatal internal_error alert (2, 80)");

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException again = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        await stream.DisposeAsync();
        Tls12OutgoingMessage? answer = await server.ReceiveAsync();
        Diagnostics.Act("failure", $"{failure.Alert}, from server {failure.IsFromServer}; again from server {again.IsFromServer}; client answered {Describe(answer)}");

        Diagnostics.Assert("alert", TlsAlertDescription.InternalError, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.InternalError, failure.Alert);
        Assert.IsTrue(failure.IsFromServer);
        Assert.IsTrue(again.IsFromServer);
        Assert.IsNull(answer);
    }

    [TestMethod]
    public async Task AlertTheTransportCannotCarryStillFailsTheStream()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        WriteFailingTransport transport = new(clientEnd);
        Tls12RecordTestServer server = new(serverEnd, new Tls12TestServer(TestServerCredential.Ed25519()));
        Task<Tls12ConnectResult> pending = Tls12ClientConnection.ConnectAsync(
            transport, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None);
        using (Diagnostics.Phase("handshake"))
        {
            await server.HandshakeAsync();
        }

        await using Tls12ClientStream stream = (await pending).Stream!;
        byte[] record = server.Protect(TlsContentType.ApplicationData, "data"u8.ToArray());
        record[^1] ^= 0x01;
        await server.SendRawAsync(record);
        transport.FailWrites = true;
        Diagnostics.Arrange("server sends", "a corrupted record while the client's transport fails every write");

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        Diagnostics.Act("failure", $"{failure.Alert}, from server {failure.IsFromServer}");

        Diagnostics.Assert("alert", TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
    }

    [TestMethod]
    public async Task HelloRequestWithABodyIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, "HelloRequest with a 1-byte body", server => server.SendAsync(TlsContentType.Handshake, [0, 0, 0, 1, 0]));

    [TestMethod]
    public async Task HandshakeMessageOtherThanHelloRequestIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "empty Finished handshake message", server => server.SendAsync(TlsContentType.Handshake, [20, 0, 0, 0]));

    [TestMethod]
    public async Task UnknownHandshakeMessageIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "handshake message of unknown type 99", server => server.SendAsync(TlsContentType.Handshake, [99, 0, 0, 0]));

    [TestMethod]
    public async Task ChangeCipherSpecAfterTheHandshakeIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "ChangeCipherSpec after the handshake", server => server.SendAsync(TlsContentType.ChangeCipherSpec, [1]));

    [TestMethod]
    public async Task AlertOfThreeBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, "alert record of 3 bytes", server => server.SendAsync(TlsContentType.Alert, [2, 80, 0]));

    [TestMethod]
    public async Task RecordOfAnotherVersionIsAProtocolVersionError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.ProtocolVersion, "empty record header with version 0x0301", server => server.SendRawAsync([0x17, 0x03, 0x01, 0x00, 0x00]));

    [TestMethod]
    public async Task RecordLongerThanAProtectedRecordMayBeIsARecordOverflow() =>
        await AssertClientAnswersAsync(TlsAlertDescription.RecordOverflow, "record of 0x4801 bytes", server => server.SendRawAsync([0x17, 0x03, 0x03, 0x48, 0x01, .. new byte[0x4801]]));

    [TestMethod]
    public async Task EmptyReadsAndWritesMoveNothing()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        Diagnostics.Arrange("calls", "empty read, empty write, 1-byte write at offset 1, 2-byte read at offset 1");

        int read = await stream.ReadAsync(Memory<byte>.Empty);
        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty);
        await stream.WriteAsync(new byte[] { 0, 7, 0 }, 1, 1);
        await server.SendAsync(TlsContentType.ApplicationData, [9]);
        byte[] buffer = new byte[3];
        int count = await stream.ReadAsync(buffer, 1, 2);
        await stream.FlushAsync();
        stream.Flush();
        byte[] received = await server.ReceiveApplicationDataAsync(1);
        Diagnostics.Act("results", $"empty read {read}, server received {Convert.ToHexString(received)}, read count {count}, buffer {Convert.ToHexString(buffer)}");

        Diagnostics.Diff("buffer", [0, 9, 0], buffer);
        Assert.AreEqual(0, read);
        CollectionAssert.AreEqual(new byte[] { 7 }, received);
        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new byte[] { 0, 9, 0 }, buffer);
    }

    [TestMethod]
    public async Task ShutdownSendsCloseNotifyOnceAndStopsWritesButNotReads()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        Diagnostics.Arrange("calls", "shutdown twice, then the server sends [5]");

        await stream.ShutdownAsync();
        await stream.ShutdownAsync();
        Tls12OutgoingMessage alert = (await server.ReceiveAsync())!;
        await server.SendAsync(TlsContentType.ApplicationData, [5]);
        byte[] read = new byte[1];
        await stream.ReadExactlyAsync(read);
        Diagnostics.Act("server received", Describe(alert));
        Diagnostics.Act("client read", Convert.ToHexString(read));

        Diagnostics.Diff("close_notify", [1, 0], alert.Bytes);
        Assert.AreEqual(TlsContentType.Alert, alert.ContentType);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, alert.Bytes);
        CollectionAssert.AreEqual(new byte[] { 5 }, read);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await stream.WriteAsync(new byte[1]));
    }

    [TestMethod]
    public async Task StreamCannotSeekOrWorkSynchronously()
    {
        (Tls12ClientStream client, _, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;

        Diagnostics.Act("capabilities", $"read {stream.CanRead}, write {stream.CanWrite}, seek {stream.CanSeek}");

        Diagnostics.Assert("can seek", false, stream.CanSeek);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task DisposeReleasesTheTransportOnceAndRefusesUse()
    {
        (Tls12ClientStream stream, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();

        stream.Dispose();
        stream.Dispose();
        Tls12OutgoingMessage? received = await server.ReceiveAsync();
        Diagnostics.Act("after two disposes", $"read {stream.CanRead}, write {stream.CanWrite}; server received {Describe(received)}");

        Diagnostics.Assert("server received", "nothing", Describe(received));
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanWrite);
        Assert.IsNull(received);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => ReadOnceAsync(stream));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.ShutdownAsync());
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);

    private static string Describe(Tls12OutgoingMessage? message) =>
        message is null ? "nothing" : $"{message.ContentType} {Convert.ToHexString(message.Bytes)}";

    private async Task<(Tls12ClientStream Client, Tls12RecordTestServer Server, Stream ServerEnd)> ConnectWithDiagnosticsAsync()
    {
        Diagnostics.Arrange("connection", "TLS 1.2 handshake with the in-memory server over a pipe, default settings");
        using (Diagnostics.Phase("handshake"))
        {
            return await ConnectAsync();
        }
    }

    private async Task AssertClientAnswersAsync(TlsAlertDescription expected, string sent, Func<Tls12RecordTestServer, Task> sendFromServer)
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectWithDiagnosticsAsync();
        await using Tls12ClientStream stream = client;
        Diagnostics.Arrange("server sends", sent);
        await sendFromServer(server);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription answered = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("failure", $"{failure.Alert}, from server {failure.IsFromServer}; client sent {answered}");

        Diagnostics.Assert("alert", expected, failure.Alert);
        Assert.AreEqual(expected, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(expected, answered);
    }
}
