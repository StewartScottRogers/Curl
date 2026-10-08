using System.Security.Cryptography;
using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((ushort)0x1301)]
    [DataRow((ushort)0x1302)]
    [DataRow((ushort)0x1303)]
    [DataRow((ushort)0x1304)]
    [DataRow((ushort)0x1305)]
    public async Task ApplicationDataCrossesBothWaysWithEachCipherSuite(int cipherSuite)
    {
        Diagnostics.Arrange("cipher suite", $"0x{cipherSuite:x4}");
        Diagnostics.Arrange("payloads", "40000 random bytes up, 20000 random bytes down");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("connect"))
        {
            (client, server, _) = await ConnectAsync(
                new Tls13TestServer(TestServerCredential.Ed25519()) { CipherSuite = (ushort)cipherSuite },
                DefaultSettings with { CipherSuites = [(ushort)cipherSuite] });
        }

        await using Tls13ClientStream stream = client;
        byte[] upload = RandomNumberGenerator.GetBytes(40000);
        byte[] download = RandomNumberGenerator.GetBytes(20000);

        byte[] uploaded;
        byte[] downloaded;
        using (Diagnostics.Phase("transfer"))
        {
            await stream.WriteAsync(upload);
            uploaded = await server.ReceiveApplicationDataAsync(upload.Length);
            await server.SendAsync(TlsContentType.ApplicationData, download);
            downloaded = await ReadAsync(stream, download.Length);
        }

        Diagnostics.Act("bytes uploaded, downloaded", $"{uploaded.Length}, {downloaded.Length}");
        Diagnostics.Act("negotiated suite", $"0x{stream.Handshake.CipherSuite!.Code:x4}");
        Diagnostics.Diff("uploaded", upload, uploaded);
        Diagnostics.Diff("downloaded", download, downloaded);
        Diagnostics.Assert("negotiated suite", cipherSuite, (int)stream.Handshake.CipherSuite!.Code);
        CollectionAssert.AreEqual(upload, uploaded);
        CollectionAssert.AreEqual(download, downloaded);
        Assert.AreEqual(cipherSuite, stream.Handshake.CipherSuite!.Code);
    }

    [TestMethod]
    public async Task ServerKeyUpdateRequestingAnUpdateIsAnsweredBeforeTheClientKeysMoveOn()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        Diagnostics.Arrange("server sends", "KeyUpdate(update_requested), then \"after\"; client then writes \"reply\"");

        await server.SendKeyUpdateAsync(requestUpdate: true);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] afterUpdate = await ReadAsync(stream, 5);
        Tls13RecordContent answer = (await server.ReceiveAsync())!;
        server.UpdateReadKeys();
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        Diagnostics.Bytes("read after the update", afterUpdate);
        Diagnostics.Act("client's answer type", answer.Type);
        Diagnostics.Bytes("client's answer", answer.Content);
        Diagnostics.Bytes("reply the server read", reply);
        Diagnostics.Diff("read after the update", "after"u8, afterUpdate);
        Diagnostics.Assert("answer type", TlsContentType.Handshake, answer.Type);
        Diagnostics.Diff("answer", new byte[] { 24, 0, 0, 1, 0 }, answer.Content);
        Diagnostics.Diff("reply", "reply"u8, reply);
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
        Diagnostics.Arrange("server sends", "KeyUpdate(update_not_requested), then \"after\"; client then writes \"reply\"");

        await server.SendKeyUpdateAsync(requestUpdate: false);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] afterUpdate = await ReadAsync(stream, 5);
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        Diagnostics.Act("bytes read, bytes the server read", $"{afterUpdate.Length}, {reply.Length}");
        Diagnostics.Bytes("read after the update", afterUpdate);
        Diagnostics.Bytes("reply the server read", reply);
        Diagnostics.Diff("read after the update", "after"u8, afterUpdate);
        Diagnostics.Diff("reply", "reply"u8, reply);
        CollectionAssert.AreEqual("after"u8.ToArray(), afterUpdate);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
    }

    [TestMethod]
    public async Task UpdateKeysSendsAKeyUpdateAndWritesUnderTheNextKeys()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        Diagnostics.Arrange("client", "UpdateKeysAsync(requestServerUpdate: true); server answers, sends \"down\"; client writes \"up\"");

        await stream.UpdateKeysAsync(requestServerUpdate: true);
        Tls13RecordContent keyUpdate = (await server.ReceiveAsync())!;
        server.UpdateReadKeys();
        await server.SendKeyUpdateAsync(requestUpdate: false);
        await server.SendAsync(TlsContentType.ApplicationData, "down"u8.ToArray());
        byte[] down = await ReadAsync(stream, 4);
        await stream.WriteAsync("up"u8.ToArray());
        byte[] up = await server.ReceiveApplicationDataAsync(2);

        Diagnostics.Act("client's KeyUpdate request byte", keyUpdate.Content[^1]);
        Diagnostics.Bytes("client's KeyUpdate", keyUpdate.Content);
        Diagnostics.Bytes("down", down);
        Diagnostics.Bytes("up", up);
        Diagnostics.Diff("KeyUpdate", new byte[] { 24, 0, 0, 1, 1 }, keyUpdate.Content);
        Diagnostics.Diff("down", "down"u8, down);
        Diagnostics.Diff("up", "up"u8, up);
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
        Diagnostics.Arrange("server", "sent \"last\", then closed the transport without close_notify");

        byte[] last = await ReadAsync(stream, 4);
        int end = await stream.ReadAsync(new byte[10]);
        int again = await stream.ReadAsync(new byte[10]);

        Diagnostics.Bytes("last", last);
        Diagnostics.Act("reads at the end", $"{end}, {again}");
        Diagnostics.Act("CloseNotifyReceived", stream.CloseNotifyReceived);
        Diagnostics.Diff("last", "last"u8, last);
        Diagnostics.Assert("bytes read at the end", 0, end);
        Diagnostics.Assert("bytes read again", 0, again);
        Diagnostics.Assert("CloseNotifyReceived", false, stream.CloseNotifyReceived);
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
        Diagnostics.Arrange("server", "sent the first 10 bytes of a protected record, then closed the transport");

        int end = await stream.ReadAsync(new byte[10]);

        Diagnostics.Act("bytes read", end);
        Diagnostics.Act("CloseNotifyReceived", stream.CloseNotifyReceived);
        Diagnostics.Assert("bytes read", 0, end);
        Diagnostics.Assert("CloseNotifyReceived", false, stream.CloseNotifyReceived);
        Assert.AreEqual(0, end);
        Assert.IsFalse(stream.CloseNotifyReceived);
    }

    [TestMethod]
    public async Task CloseNotifyEndsTheStream()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        await server.SendAsync(TlsContentType.Alert, [1, 0]);
        Diagnostics.Arrange("server sent alert", "[01 00] (warning close_notify)");

        int end = await stream.ReadAsync(new byte[10]);

        Diagnostics.Act("bytes read", end);
        Diagnostics.Act("CloseNotifyReceived", stream.CloseNotifyReceived);
        Diagnostics.Assert("bytes read", 0, end);
        Diagnostics.Assert("CloseNotifyReceived", true, stream.CloseNotifyReceived);
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
        Diagnostics.Arrange("record", "protected application data \"data\", last tag byte XORed with 0x01");
        Diagnostics.Bytes("record with its last tag bit flipped", record);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription sent = await ReceiveFatalAlertAsync(server);
        TlsAlertException readAgain = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException write = await Assert.ThrowsExactlyAsync<TlsAlertException>(async () => await stream.WriteAsync(new byte[1]));

        Diagnostics.Act("failure", failure.Message);
        Diagnostics.Act("alert the client sent", sent);
        Diagnostics.Act("read again, write", $"{readAgain.Alert}, {write.Alert}");
        Diagnostics.Assert("failure alert", TlsAlertDescription.BadRecordMac, failure.Alert);
        Diagnostics.Assert("IsFromServer", false, failure.IsFromServer);
        Diagnostics.Assert("alert sent", TlsAlertDescription.BadRecordMac, sent);
        Diagnostics.Assert("read again and write alerts", "BadRecordMac, BadRecordMac", $"{readAgain.Alert}, {write.Alert}");
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
        Diagnostics.Arrange("server sent alert", "[02 50] (fatal internal_error)");

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertException again = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        await stream.DisposeAsync();
        Tls13RecordContent? answer = await server.ReceiveAsync();

        Diagnostics.Act("failure", failure.Message);
        Diagnostics.Act("again IsFromServer", again.IsFromServer);
        Diagnostics.Act("record the server received", answer?.Type.ToString() ?? "none");
        Diagnostics.Assert("failure alert", TlsAlertDescription.InternalError, failure.Alert);
        Diagnostics.Assert("IsFromServer", true, failure.IsFromServer);
        Diagnostics.Assert("answer", "none", answer?.Type.ToString() ?? "none");
        Assert.AreEqual(TlsAlertDescription.InternalError, failure.Alert);
        Assert.IsTrue(failure.IsFromServer);
        Assert.AreEqual("The server sent the TLS alert InternalError.", failure.Message);
        Assert.IsTrue(again.IsFromServer);
        Assert.IsNull(answer);
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
        Diagnostics.Arrange("transport", "RFC 8448 server records, application data record corrupted; writes fail");
        Diagnostics.Bytes("corrupted record", corrupted);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Diagnostics.Act("failure", failure.Message);
        Diagnostics.Assert("failure alert", TlsAlertDescription.BadRecordMac, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.BadRecordMac, failure.Alert);
    }

    [TestMethod]
    public async Task NewSessionTicketSplitAcrossRecordsReachesTheHandshake()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        byte[] ticket = Convert.FromHexString(Rfc8448Messages.SimpleNewSessionTicket);
        Diagnostics.Arrange("ticket", $"{ticket.Length} bytes, split after 100; then an empty and a one-byte data record");

        await server.SendAsync(TlsContentType.Handshake, ticket[..100]);
        await server.SendAsync(TlsContentType.Handshake, ticket[100..]);
        await server.SendAsync(TlsContentType.ApplicationData, []);
        await server.SendAsync(TlsContentType.ApplicationData, "x"u8.ToArray());
        byte[] data = await ReadAsync(stream, 1);

        Diagnostics.Bytes("data", data);
        Diagnostics.Act("tickets received", stream.Handshake.ReceivedTickets.Count);
        Diagnostics.Diff("data", "x"u8, data);
        Diagnostics.Assert("tickets received", 1, stream.Handshake.ReceivedTickets.Count);
        CollectionAssert.AreEqual("x"u8.ToArray(), data);
        Assert.HasCount(1, stream.Handshake.ReceivedTickets);
    }

    [TestMethod]
    public async Task PlaintextRecordAfterTheHandshakeIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "plaintext alert record [15 03 03 00 02 02 28]", server => server.SendRawAsync([0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28]));

    [TestMethod]
    public async Task ProtectedChangeCipherSpecIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "protected ChangeCipherSpec [01]", server => server.SendAsync(TlsContentType.ChangeCipherSpec, [1]));

    [TestMethod]
    public async Task RecordOfNothingButPaddingIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "protected record of content type 0 and no content", server => server.SendAsync(0, []));

    [TestMethod]
    public async Task AlertOfThreeBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, "protected alert [02 50 00]", server => server.SendAsync(TlsContentType.Alert, [2, 80, 0]));

    [TestMethod]
    public async Task EmptyHandshakeRecordIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "protected empty handshake record", server => server.SendAsync(TlsContentType.Handshake, []));

    [TestMethod]
    public async Task UnknownHandshakeMessageIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "handshake message type 99 [63 00 00 00]", server => server.SendAsync(TlsContentType.Handshake, [99, 0, 0, 0]));

    [TestMethod]
    public async Task HandshakeMessageTheHandshakeRefusesAfterCompletionIsUnexpected() =>
        await AssertClientAnswersAsync(
            TlsAlertDescription.UnexpectedMessage,
            "RFC 8448 server Finished after the handshake",
            server => server.SendAsync(TlsContentType.Handshake, Convert.FromHexString(Rfc8448Messages.SimpleServerFinished)));

    [TestMethod]
    public async Task KeyUpdateThatDoesNotEndItsRecordIsUnexpected() =>
        await AssertClientAnswersAsync(TlsAlertDescription.UnexpectedMessage, "KeyUpdate followed by more bytes [18 00 00 01 00 04 00]", server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 1, 0, 4, 0]));

    [TestMethod]
    public async Task KeyUpdateOfTwoBytesIsADecodeError() =>
        await AssertClientAnswersAsync(TlsAlertDescription.DecodeError, "two-byte KeyUpdate [18 00 00 02 00 00]", server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 2, 0, 0]));

    [TestMethod]
    public async Task KeyUpdateWithAnUnknownRequestIsAnIllegalParameter() =>
        await AssertClientAnswersAsync(TlsAlertDescription.IllegalParameter, "KeyUpdate with request 2 [18 00 00 01 02]", server => server.SendAsync(TlsContentType.Handshake, [24, 0, 0, 1, 2]));

    [TestMethod]
    public async Task RecordLongerThanAProtectedRecordMayBeIsARecordOverflow() =>
        await AssertClientAnswersAsync(TlsAlertDescription.RecordOverflow, "record header claiming 0x4101 bytes", server => server.SendRawAsync([0x17, 0x03, 0x03, 0x41, 0x01]));

    [TestMethod]
    public async Task EmptyReadsAndWritesMoveNothing()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        Diagnostics.Arrange("calls", "empty read, empty write, write of [07] at offset 1, read into a 3-byte buffer at offset 1");

        int read = await stream.ReadAsync(Memory<byte>.Empty);
        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty);
        await stream.WriteAsync(new byte[] { 0, 7, 0 }, 1, 1);
        await server.SendAsync(TlsContentType.ApplicationData, [9]);
        byte[] buffer = new byte[3];
        int count = await stream.ReadAsync(buffer, 1, 2);
        await stream.FlushAsync();
        stream.Flush();
        byte[] received = await server.ReceiveApplicationDataAsync(1);

        Diagnostics.Act("empty read returned", read);
        Diagnostics.Bytes("server received", received);
        Diagnostics.Act("offset read returned", count);
        Diagnostics.Bytes("buffer", buffer);
        Diagnostics.Assert("empty read", 0, read);
        Diagnostics.Diff("server received", new byte[] { 7 }, received);
        Diagnostics.Assert("offset read", 1, count);
        Diagnostics.Diff("buffer", new byte[] { 0, 9, 0 }, buffer);
        Assert.AreEqual(0, read);
        CollectionAssert.AreEqual(new byte[] { 7 }, received);
        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new byte[] { 0, 9, 0 }, buffer);
    }

    [TestMethod]
    public async Task ShutdownSendsCloseNotifyOnceAndStopsWritesButNotReads()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        Diagnostics.Arrange("client", "ShutdownAsync twice; server then sends [05]");

        await stream.ShutdownAsync();
        await stream.ShutdownAsync();
        Tls13RecordContent alert = (await server.ReceiveAsync())!;
        await server.SendAsync(TlsContentType.ApplicationData, [5]);
        byte[] read = await ReadAsync(stream, 1);

        Diagnostics.Act("record type", alert.Type);
        Diagnostics.Bytes("record content", alert.Content);
        Diagnostics.Bytes("read", read);
        Diagnostics.Assert("record type", TlsContentType.Alert, alert.Type);
        Diagnostics.Diff("close_notify", new byte[] { 1, 0 }, alert.Content);
        Diagnostics.Diff("read", new byte[] { 5 }, read);
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
        Diagnostics.Arrange("stream", "connected");

        Diagnostics.Act("CanRead, CanWrite, CanSeek", $"{stream.CanRead}, {stream.CanWrite}, {stream.CanSeek}");
        Diagnostics.Assert("CanRead, CanWrite, CanSeek", "True, True, False", $"{stream.CanRead}, {stream.CanWrite}, {stream.CanSeek}");
        Diagnostics.Assert("NotSupportedExceptions expected", "Length, Position get and set, Seek, SetLength, Read, Write", "asserted below");
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
        Diagnostics.Arrange("stream", "connected, then disposed twice");

        stream.Dispose();
        stream.Dispose();
        Tls13RecordContent? received = await server.ReceiveAsync();

        Diagnostics.Act("CanRead, CanWrite", $"{stream.CanRead}, {stream.CanWrite}");
        Diagnostics.Act("record the server received", received?.Type.ToString() ?? "none");
        Diagnostics.Assert("CanRead, CanWrite", "False, False", $"{stream.CanRead}, {stream.CanWrite}");
        Diagnostics.Assert("record the server received", "none", received?.Type.ToString() ?? "none");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanWrite);
        Assert.IsNull(received);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => ReadOnceAsync(stream));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.UpdateKeysAsync(false));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => stream.ShutdownAsync());
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);

    private async Task AssertClientAnswersAsync(TlsAlertDescription expected, string sent, Func<Tls13RecordTestServer, Task> sendFromServer)
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;
        Diagnostics.Arrange("server sends", sent);
        await sendFromServer(server);

        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription answer = await ReceiveFatalAlertAsync(server);

        Diagnostics.Act("failure", failure.Message);
        Diagnostics.Act("alert the client sent", answer);
        Diagnostics.Assert("failure alert", expected, failure.Alert);
        Diagnostics.Assert("IsFromServer", false, failure.IsFromServer);
        Diagnostics.Assert("alert sent", expected, answer);
        Assert.AreEqual(expected, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(expected, answer);
    }
}
