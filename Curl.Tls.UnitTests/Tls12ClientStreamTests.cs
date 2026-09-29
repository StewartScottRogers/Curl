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
    [TestMethod]
    public async Task TransportEndingWithoutCloseNotifyEndsTheStreamWithoutTheCloseNotifyFlag()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, Stream serverEnd) = await ConnectAsync();
        await using Tls12ClientStream stream = client;
        await server.SendAsync(TlsContentType.ApplicationData, "last"u8.ToArray());
        await serverEnd.DisposeAsync();

        byte[] last = new byte[4];
        await stream.ReadExactlyAsync(last);
        int end = await stream.ReadAsync(new byte[10]);
        int again = await stream.ReadAsync(new byte[10]);

        CollectionAssert.AreEqual("last"u8.ToArray(), last);
        Assert.AreEqual(0, end);
        Assert.AreEqual(0, again);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task TransportEndingInsideARecordEndsTheStream()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, Stream serverEnd) = await ConnectAsync();
        await using Tls12ClientStream stream = client;
        await server.SendRawAsync(server.Protect(TlsContentType.ApplicationData, "cut"u8.ToArray())[..10]);
        await serverEnd.DisposeAsync();

        int end = await stream.ReadAsync(new byte[10]);

        Assert.AreEqual(0, end);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task HelloRequestsWholeOrSplitAcrossRecordsAreIgnored()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;

        await server.SendAsync(TlsContentType.Handshake, [0, 0, 0, 0, 0, 0]);
        await server.SendAsync(TlsContentType.Handshake, [0, 0]);
        await server.SendAsync(TlsContentType.ApplicationData, "x"u8.ToArray());
        byte[] data = new byte[1];
        await stream.ReadExactlyAsync(data);

        CollectionAssert.AreEqual("x"u8.ToArray(), data);
    }

    [TestMethod]
    public async Task CorruptedRecordFailsTheStreamWithBadRecordMac()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;
        byte[] record = server.Protect(TlsContentType.ApplicationData, "data"u8.ToArray());
        record[^1] ^= 0x01;
        await server.SendRawAsync(record);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription sent = await ReceiveFatalAlertAsync(server);
        TlsAlertException readAgain = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException write = await Assert.ThrowsExactlyAsync<TlsAlertException>(async () => await stream.WriteAsync(new byte[1]));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, sent);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, readAgain.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, write.Alert);
    }

    [TestMethod]
    public async Task FatalAlertFromTheServerFailsTheStreamWithoutAnAnswer()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;
        await server.SendAsync(TlsContentType.Alert, [2, 80]);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException again = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        await stream.DisposeAsync();

        Assert.AreEqual(TlsAlertDescription.InternalError, failure.Alert);
        Assert.IsTrue(failure.IsFromServer);
        Assert.IsTrue(again.IsFromServer);
        Assert.IsNull(await server.ReceiveAsync());
    }

    [TestMethod]
    public async Task AlertTheTransportCannotCarryStillFailsTheStream()
    {
        (Stream clientEnd, Stream serverEnd) = InMemoryPipe.Create();
        WriteFailingTransport transport = new(clientEnd);
        Tls12RecordTestServer server = new(serverEnd, new Tls12TestServer(TestServerCredential.Ed25519()));
        Task<Tls12ConnectResult> pending = Tls12ClientConnection.ConnectAsync(
            transport, DefaultSettings, SystemTlsRandomSource.Instance, new RecordingCertificateVerifier(), CancellationToken.None);
        await server.HandshakeAsync();
        await using Tls12ClientStream stream = (await pending).Stream!;
        byte[] record = server.Protect(TlsContentType.ApplicationData, "data"u8.ToArray());
        record[^1] ^= 0x01;
        await server.SendRawAsync(record);
        transport.FailWrites = true;

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
    }

    [TestMethod]
    public async Task HelloRequestWithABodyIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, server => server.SendAsync(TlsContentType.Handshake, [0, 0, 0, 1, 0]));

    [TestMethod]
    public async Task HandshakeMessageOtherThanHelloRequestIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.Handshake, [20, 0, 0, 0]));

    [TestMethod]
    public async Task UnknownHandshakeMessageIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.Handshake, [99, 0, 0, 0]));

    [TestMethod]
    public async Task ChangeCipherSpecAfterTheHandshakeIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.ChangeCipherSpec, [1]));

    [TestMethod]
    public async Task AlertOfThreeBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, server => server.SendAsync(TlsContentType.Alert, [2, 80, 0]));

    [TestMethod]
    public async Task RecordOfAnotherVersionIsAProtocolVersionError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.ProtocolVersion, server => server.SendRawAsync([0x17, 0x03, 0x01, 0x00, 0x00]));

    [TestMethod]
    public async Task RecordLongerThanAProtectedRecordMayBeIsARecordOverflow() =>
        await AssertClientAnswersAsync(TlsAlertDescription.RecordOverflow, server => server.SendRawAsync([0x17, 0x03, 0x03, 0x48, 0x01, .. new byte[0x4801]]));

    [TestMethod]
    public async Task EmptyReadsAndWritesMoveNothing()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;

        int read = await stream.ReadAsync(Memory<byte>.Empty);
        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty);
        await stream.WriteAsync(new byte[] { 0, 7, 0 }, 1, 1);
        await server.SendAsync(TlsContentType.ApplicationData, [9]);
        byte[] buffer = new byte[3];
        int count = await stream.ReadAsync(buffer, 1, 2);
        await stream.FlushAsync();
        stream.Flush();

        Assert.AreEqual(0, read);
        CollectionAssert.AreEqual(new byte[] { 7 }, await server.ReceiveApplicationDataAsync(1));
        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new byte[] { 0, 9, 0 }, buffer);
    }

    [TestMethod]
    public async Task ShutdownSendsCloseNotifyOnceAndStopsWritesButNotReads()
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;

        await stream.ShutdownAsync();
        await stream.ShutdownAsync();
        Tls12OutgoingMessage alert = (await server.ReceiveAsync())!;
        await server.SendAsync(TlsContentType.ApplicationData, [5]);
        byte[] read = new byte[1];
        await stream.ReadExactlyAsync(read);

        Assert.AreEqual(TlsContentType.Alert, alert.ContentType);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, alert.Bytes);
        CollectionAssert.AreEqual(new byte[] { 5 }, read);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await stream.WriteAsync(new byte[1]));
    }

    [TestMethod]
    public async Task StreamCannotSeekOrWorkSynchronously()
    {
        (Tls12ClientStream client, _, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;

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
        (Tls12ClientStream stream, Tls12RecordTestServer server, _) = await ConnectAsync();

        stream.Dispose();
        stream.Dispose();

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanWrite);
        Assert.IsNull(await server.ReceiveAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => ReadOnceAsync(stream));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.ShutdownAsync());
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);

    private static async Task AssertClientAnswersAsync(TlsAlertDescription expected, Func<Tls12RecordTestServer, Task> sendFromServer)
    {
        (Tls12ClientStream client, Tls12RecordTestServer server, _) = await ConnectAsync();
        await using Tls12ClientStream stream = client;
        await sendFromServer(server);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(expected, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(expected, await ReceiveFatalAlertAsync(server));
    }
}
