using static Curl.Tls.Rfc8448Messages;
using static Curl.Tls.Rfc8448Records;

namespace Curl.Tls;

/// <summary>
/// Replays RFC 8448 section 3's protected records: each record protected and unprotected
/// under the trace's traffic secrets, and the whole connection over a byte stream, with
/// the client's randoms and key share injected, so every record the client writes is the
/// trace's byte for byte.
/// </summary>
[TestClass]
public sealed class Rfc8448RecordTests
{
    // Section 3: {client} create an ephemeral x25519 key pair, and the ClientHello's random.
    private const string ClientPrivateKey = "49af42ba7f7994852d713ef2784bcbcaa7911de26adc5642cb634540e7ea5005";
    private const string ClientRandom = "cb34ecb1e78163ba1c38c6dacb196a6dffa21a8d9912ec18a2ef6283024dece7";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(ServerHandshakeTrafficSecret, SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished, ServerHandshakeFlight, 22)]
    [DataRow(ClientHandshakeTrafficSecret, SimpleClientFinished, ClientFinished, 22)]
    [DataRow(ServerApplicationTrafficSecret, SimpleNewSessionTicket, ServerNewSessionTicket, 22)]
    public void ProtectReproducesTheTraceFirstRecordUnderEachSecret(string secret, string content, string record, int contentType)
    {
        using Tls13RecordProtection protection = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128GcmSha256, Hex(secret));

        byte[] protectedRecord = protection.Protect((TlsContentType)contentType, Hex(content));

        AssertHex(record, protectedRecord);
        Assert.AreEqual(1ul, protection.SequenceNumber);
    }

    [TestMethod]
    public void ProtectReproducesTheTraceClientApplicationDataAndCloseNotify()
    {
        using Tls13RecordProtection protection = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128GcmSha256, Hex(ClientApplicationTrafficSecret));

        AssertHex(ClientApplicationData, protection.Protect(TlsContentType.ApplicationData, ApplicationData));
        AssertHex(ClientCloseNotify, protection.Protect(TlsContentType.Alert, [1, 0]));
    }

    [TestMethod]
    public void UnprotectReadsTheTraceServerApplicationRecordsInOrder()
    {
        using Tls13RecordProtection protection = Tls13RecordProtection.Create(Tls13CipherSuite.Aes128GcmSha256, Hex(ServerApplicationTrafficSecret));

        Tls13RecordContent ticket = protection.Unprotect(Hex(ServerNewSessionTicket)).Value;
        Tls13RecordContent data = protection.Unprotect(Hex(ServerApplicationData)).Value;
        Tls13RecordContent alert = protection.Unprotect(Hex(ServerCloseNotify)).Value;

        Assert.AreEqual(TlsContentType.Handshake, ticket.Type);
        AssertHex(SimpleNewSessionTicket, ticket.Content);
        Assert.AreEqual(TlsContentType.ApplicationData, data.Type);
        CollectionAssert.AreEqual(ApplicationData, data.Content);
        Assert.AreEqual(TlsContentType.Alert, alert.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, alert.Content);
        Assert.AreEqual(3ul, protection.SequenceNumber);
    }

    [TestMethod]
    public async Task ConnectionWritesEveryClientRecordOfTheTrace()
    {
        ScriptedTransport transport = new(Hex(ServerHelloRecord + ServerHandshakeFlight + ServerNewSessionTicket + ServerApplicationData + ServerCloseNotify));

        Tls13ConnectResult result = await Tls13ClientConnection.ConnectAsync(
            transport, TraceSettings(), TraceRandom(), new RecordingCertificateVerifier(), TestContext.CancellationToken);
        await using Tls13ClientStream stream = result.Stream!;
        AssertHex(ClientHelloRecord + ClientFinished, transport.Written);

        await stream.WriteAsync(ApplicationData, TestContext.CancellationToken);
        byte[] received = new byte[100];
        int count = await stream.ReadAsync(received, TestContext.CancellationToken);
        await stream.ShutdownAsync(TestContext.CancellationToken);
        int end = await stream.ReadAsync(received, TestContext.CancellationToken);

        AssertHex(ClientHelloRecord + ClientFinished + ClientApplicationData + ClientCloseNotify, transport.Written);
        CollectionAssert.AreEqual(ApplicationData, received[..count]);
        Assert.HasCount(1, stream.Handshake.ReceivedTickets);
        Assert.AreEqual(0, end);
        Assert.IsTrue(stream.CloseNotifyReceived);
        Assert.IsNull(result.Failure);
        Assert.IsTrue(result.Succeeded);
    }

    internal static Tls13ClientSettings TraceSettings() => new()
    {
        ServerName = "server",
        CipherSuites = [0x1301, 0x1303, 0x1302],
        SupportedGroups =
        [
            TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1,
            TlsNamedGroup.Ffdhe2048, TlsNamedGroup.Ffdhe3072, TlsNamedGroup.Ffdhe4096, TlsNamedGroup.Ffdhe6144, TlsNamedGroup.Ffdhe8192,
        ],
        KeyShareGroups = [TlsNamedGroup.X25519],
        SignatureAlgorithms = [0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202],
        ExtensionOrder =
        [
            TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.SessionTicket,
            TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms,
            TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit,
        ],
        FixedExtensions =
        [
            new(TlsExtensionType.RenegotiationInfo, [0x00]),
            new(TlsExtensionType.SessionTicket, []),
            new(TlsExtensionType.PskKeyExchangeModes, [0x01, 0x01]),
            new(TlsExtensionType.RecordSizeLimit, [0x40, 0x01]),
        ],
    };

    internal static ReplayTlsRandomSource TraceRandom() => new([Hex(ClientRandom)], [new X25519KeyShare(Hex(ClientPrivateKey))]);

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static void AssertHex(string expected, byte[] actual) => Assert.AreEqual(expected, Convert.ToHexStringLower(actual));
}
