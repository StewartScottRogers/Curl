using System.Security.Cryptography;
using static Curl.Tls.Tls13PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Drives a connected <see cref="Tls13ClientStream" /> against the in-memory server over a
/// pipe: application data both ways for each suite, KeyUpdate in both directions, the end
/// of the stream with and without <c>close_notify</c>, and every record the client
/// refuses, each answered with the alert RFC 8446 names.
/// </summary>
[TestClass]
public sealed class Tls13ClientStreamTests
{
    [TestMethod]
    [DataRow((ushort)0x1301)]
    [DataRow((ushort)0x1302)]
    [DataRow((ushort)0x1303)]
    [DataRow((ushort)0x1304)]
    [DataRow((ushort)0x1305)]
    public async Task ApplicationDataCrossesBothWaysWithEachCipherSuite(int cipherSuite)
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(
            new Tls13TestServer(TestServerCredential.Ed25519()) { CipherSuite = (ushort)cipherSuite },
            DefaultSettings with { CipherSuites = [(ushort)cipherSuite] });
        await using Tls13ClientStream stream = client;
        byte[] upload = RandomNumberGenerator.GetBytes(40000);
        byte[] download = RandomNumberGenerator.GetBytes(20000);

        await stream.WriteAsync(upload);
        byte[] uploaded = await server.ReceiveApplicationDataAsync(upload.Length);
        await server.SendAsync(TlsContentType.ApplicationData, download);
        byte[] downloaded = await ReadAsync(stream, download.Length);

        CollectionAssert.AreEqual(upload, uploaded);
        CollectionAssert.AreEqual(download, downloaded);
        Assert.AreEqual(cipherSuite, stream.Handshake.CipherSuite!.Code);
    }

    [TestMethod]
    public async Task ServerKeyUpdateRequestingAnUpdateIsAnsweredBeforeTheClientKeysMoveOn()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

        await server.SendKeyUpdateAsync(requestUpdate: true);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] afterUpdate = await ReadAsync(stream, 5);
        Tls13RecordContent answer = (await server.ReceiveAsync())!;
        server.UpdateReadKeys();
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        CollectionAssert.AreEqual("after"u8.ToArray(), afterUpdate);
        Assert.AreEqual(TlsContentType.Handshake, answer.Type);
        CollectionAssert.AreEqual(new byte[] { 24, 0, 0, 1, 0 }, answer.Content);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
    }

    [TestMethod]
    public async Task ServerKeyUpdateWithoutARequestMovesOnlyTheReadKeys()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

        await server.SendKeyUpdateAsync(requestUpdate: false);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] afterUpdate = await ReadAsync(stream, 5);
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        CollectionAssert.AreEqual("after"u8.ToArray(), afterUpdate);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
    }

    [TestMethod]
    public async Task UpdateKeysSendsAKeyUpdateAndWritesUnderTheNextKeys()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

        await stream.UpdateKeysAsync(requestServerUpdate: true);
        Tls13RecordContent keyUpdate = (await server.ReceiveAsync())!;
        server.UpdateReadKeys();
        await server.SendKeyUpdateAsync(requestUpdate: false);
        await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
        byte[] down = await ReadAsync(stream, 4);
        await stream.WriteAsync("up"u8.ToArray());
        byte[] up = await server.ReceiveApplicationDataAsync(2);

        CollectionAssert.AreEqual(new byte[] { 24, 0, 0, 1, 1 }, keyUpdate.Content);
        CollectionAssert.AreEqual("down"u8.ToArray(), down);
        CollectionAssert.AreEqual("up"u8.ToArray(), up);
    }

    [TestMethod]
    public async Task TransportEndingWithoutCloseNotifyEndsTheStreamWithoutTheCloseNotifyFlag()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, Stream serverEnd) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await server.SendAsync(TlsContentType.ApplicationData, "last"u8.ToArray());
        await serverEnd.DisposeAsync();

        byte[] last = await ReadAsync(stream, 4);
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
        (Tls13ClientStream client, Tls13RecordTestServer server, Stream serverEnd) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await server.SendRawAsync(server.Protect(TlsContentType.ApplicationData, "cut"u8.ToArray())[..10]);
        await serverEnd.DisposeAsync();

        int end = await stream.ReadAsync(new byte[10]);

        Assert.AreEqual(0, end);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task CloseNotifyEndsTheStream()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await server.SendAsync(TlsContentType.Alert, [1, 0]);

        int end = await stream.ReadAsync(new byte[10]);

        Assert.AreEqual(0, end);
        Assert.IsTrue(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task CorruptedRecordFailsTheStreamWithBadRecordMac()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        byte[] record = server.Protect(TlsContentType.ApplicationData, "data"u8.ToArray());
        record[^1] ^= 0x01;
        await server.SendRawAsync(record);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription sent = await ReceiveFatalAlertAsync(server);
        TlsAlertException readAgain = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException write = await Assert.ThrowsExactlyAsync<TlsAlertException>(async () => await stream.WriteAsync(new byte[1]));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual("The client sent the TLS alert BadRecordMac.", failure.Message);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, sent);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, readAgain.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, write.Alert);
    }

    [TestMethod]
    public async Task FatalAlertFromTheServerFailsTheStreamWithoutAnAnswer()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await server.SendAsync(TlsContentType.Alert, [2, 80]);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException again = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        await stream.DisposeAsync();

        Assert.AreEqual(TlsAlertDescription.InternalError, failure.Alert);
        Assert.IsTrue(failure.IsFromServer);
        Assert.AreEqual("The server sent the TLS alert InternalError.", failure.Message);
        Assert.IsTrue(again.IsFromServer);
        Assert.IsNull(await server.ReceiveAsync());
    }

    [TestMethod]
    public async Task AlertTheTransportCannotCarryStillFailsTheStream()
    {
        byte[] corrupted = Convert.FromHexString(Rfc8448Records.ServerApplicationData);
        corrupted[^1] ^= 0x01;
        ScriptedTransport transport = new(
            [.. Convert.FromHexString(Rfc8448Records.ServerHelloRecord + Rfc8448Records.ServerHandshakeFlight + Rfc8448Records.ServerNewSessionTicket), .. corrupted]);
        Tls13ConnectResult result = await Tls13ClientConnection.ConnectAsync(
            transport, Rfc8448RecordTests.TraceSettings(), Rfc8448RecordTests.TraceRandom(), new RecordingCertificateVerifier(), CancellationToken.None);
        await using Tls13ClientStream stream = result.Stream!;
        transport.FailWrites = true;

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
    }

    [TestMethod]
    public async Task NewSessionTicketSplitAcrossRecordsReachesTheHandshake()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        byte[] ticket = Convert.FromHexString(Rfc8448Messages.SimpleNewSessionTicket);

        await server.SendAsync(TlsContentType.Handshake, ticket[..100]);
        await server.SendAsync(TlsContentType.Handshake, ticket[100..]);
        await server.SendAsync(TlsContentType.ApplicationData, []);
        await server.SendAsync(TlsContentType.ApplicationData, "x"u8.ToArray());
        byte[] data = await ReadAsync(stream, 1);

        CollectionAssert.AreEqual("x"u8.ToArray(), data);
        Assert.HasCount(1, stream.Handshake.ReceivedTickets);
    }

    [TestMethod]
    public async Task PlaintextRecordAfterTheHandshakeIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendRawAsync([0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28]));

    [TestMethod]
    public async Task ProtectedChangeCipherSpecIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.ChangeCipherSpec, [1]));

    [TestMethod]
    public async Task RecordOfNothingButPaddingIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(0, []));

    [TestMethod]
    public async Task AlertOfThreeBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, server => server.SendAsync(TlsContentType.Alert, [2, 80, 0]));

    [TestMethod]
    public async Task EmptyHandshakeRecordIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.Handshake, []));

    [TestMethod]
    public async Task UnknownHandshakeMessageIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.Handshake, [99, 0, 0, 0]));

    [TestMethod]
    public async Task HandshakeMessageTheHandshakeRefusesAfterCompletionIsUnexpected() =>
        await AssertClientAnswersAsync(
            TlsAlertDescription.UnexpectedMessage,
            server => server.SendAsync(TlsContentType.Handshake, Convert.FromHexString(Rfc8448Messages.SimpleServerFinished)));

    [TestMethod]
    public async Task KeyUpdateThatDoesNotEndItsRecordIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 1, 0, 4, 0]));

    [TestMethod]
    public async Task KeyUpdateOfTwoBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 2, 0, 0]));

    [TestMethod]
    public async Task KeyUpdateWithAnUnknownRequestIsAnIllegalParameter() =>
        await AssertClientAnswersAsync(TlsAlertDescription.IllegalParameter, server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 1, 2]));

    [TestMethod]
    public async Task RecordLongerThanAProtectedRecordMayBeIsARecordOverflow() =>
        await AssertClientAnswersAsync(TlsAlertDescription.RecordOverflow, server => server.SendRawAsync([0x17, 0x03, 0x03, 0x41, 0x01]));

    [TestMethod]
    public async Task EmptyReadsAndWritesMoveNothing()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

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
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

        await stream.ShutdownAsync();
        await stream.ShutdownAsync();
        Tls13RecordContent alert = (await server.ReceiveAsync())!;
        await server.SendAsync(TlsContentType.ApplicationData, [5]);
        byte[] read = await ReadAsync(stream, 1);

        Assert.AreEqual(TlsContentType.Alert, alert.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, alert.Content);
        CollectionAssert.AreEqual(new byte[] { 5 }, read);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await stream.WriteAsync(new byte[1]));
    }

    [TestMethod]
    public async Task StreamCannotSeekOrWorkSynchronously()
    {
        (Tls13ClientStream client, _, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

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
        (Tls13ClientStream stream, Tls13RecordTestServer server, _) = await ConnectAsync();

        stream.Dispose();
        stream.Dispose();

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanWrite);
        Assert.IsNull(await server.ReceiveAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => ReadOnceAsync(stream));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.UpdateKeysAsync(false));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.ShutdownAsync());
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);

    private static async Task AssertClientAnswersAsync(TlsAlertDescription expected, Func<Tls13RecordTestServer, Task> sendFromServer)
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await sendFromServer(server);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(expected, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(expected, await ReceiveFatalAlertAsync(server));
    }
}
