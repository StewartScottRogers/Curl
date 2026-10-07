using System.Runtime.CompilerServices;
using Curl.Testing;
using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Replays RFC 8448 section 4 ("Resumed 0-RTT Handshake") as the client: the session comes
/// from the section 3 handshake's NewSessionTicket, received 6 ms before the resumption
/// (the ticket's age add is <c>fad6aac5</c>, the trace's obfuscated age <c>fad6aacb</c>),
/// and the trace's random and x25519 key are injected, so the ClientHello, its binder, the
/// early data record, EndOfEarlyData and the client's Finished are the trace's bytes.
/// </summary>
[TestClass]
public sealed class Rfc8448ResumedHandshakeTests
{
    // Section 3: {client} create an ephemeral x25519 key pair, and the ClientHello's random.
    private const string SimpleClientPrivateKey = "49af42ba7f7994852d713ef2784bcbcaa7911de26adc5642cb634540e7ea5005";
    private const string SimpleClientRandom = "cb34ecb1e78163ba1c38c6dacb196a6dffa21a8d9912ec18a2ef6283024dece7";

    // Section 4: {client} create an ephemeral x25519 key pair, and the ClientHello's random.
    private const string ResumedClientPrivateKey = "bff91188283846dd6a2134ef7180ca2b0b14fb10dce707b5098c0dddc813b2df";
    private const string ResumedClientRandom = "1bc3ceb6bbe39cff938355b5a50adb6db21b7a6af649d7b4bc419d7876487d95";

    private static readonly ushort[] TraceSignatureAlgorithms =
        [0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202];

    private static readonly ushort[] TraceGroups =
    [
        TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1,
        TlsNamedGroup.Ffdhe2048, TlsNamedGroup.Ffdhe3072, TlsNamedGroup.Ffdhe4096, TlsNamedGroup.Ffdhe6144, TlsNamedGroup.Ffdhe8192,
    ];

    private static readonly TlsExtension RenegotiationInfo = new(TlsExtensionType.RenegotiationInfo, [0x00]);
    private static readonly TlsExtension SessionTicket = new(TlsExtensionType.SessionTicket, []);
    private static readonly TlsExtension PskKeyExchangeModes = new(TlsExtensionType.PskKeyExchangeModes, [0x01, 0x01]);
    private static readonly TlsExtension RecordSizeLimit = new(TlsExtensionType.RecordSizeLimit, [0x40, 0x01]);

    private static readonly DateTimeOffset TicketReceived = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SimpleHandshakeRecordsTheTicketAsASession()
    {
        TlsSessionRecord session = RecordedSimpleSession();

        Assert.AreEqual(0x0304, session.Version);
        Assert.AreEqual(0x1301, session.CipherSuite);
        AssertHex("4ecd0eb6ec3b4d87f5d6028f922ca4c5851a277fd41311c9e62d2c9492e1c4f3", session.PreSharedKey);
        Assert.AreEqual(30u, session.TicketLifetime);
        Assert.AreEqual(0xfad6aac5u, session.TicketAgeAdd);
        Assert.AreEqual(1024u, session.MaxEarlyDataSize);
        Assert.AreEqual(TicketReceived, session.ReceivedAt);
        Assert.AreEqual("server", session.ServerName);
        Assert.IsNull(session.ApplicationProtocol);
        Assert.AreEqual(TlsNamedGroup.X25519, session.Group);
        Assert.HasCount(32, session.SessionId);
        Assert.IsNotNull(session.PeerCertificate);
    }

    [TestMethod]
    public void ResumedHandshakeSendsTheTraceClientHelloAndInstallsTheEarlyTrafficSecret()
    {
        using Tls13ClientHandshake client = ResumedClient(RecordedSimpleSession());

        Tls13HandshakeOutput output = client.Start();

        AssertHex(ResumedClientHello, output.BytesToSend.Single().Bytes);
        Tls13TrafficSecret early = output.SecretsInstalled.Single();
        Assert.AreEqual(TlsEncryptionLevel.EarlyData, early.Level);
        Assert.AreEqual(TlsTrafficDirection.Write, early.Direction);
        AssertHex("3fbbe6a60deb66c30a32795aba0eff7eaa10105586e7be5c09678d63b6caab62", early.Secret);
        Assert.IsTrue(client.EarlyDataOffered);
        Assert.AreEqual(1024u, client.MaxEarlyDataSize);
        Assert.AreSame(Tls13CipherSuite.Aes128GcmSha256, client.EarlyDataCipherSuite);
    }

    // Section 4: {client} send application_data record, the six bytes "ABCDEF" under the early keys.
    [TestMethod]
    public void EarlyTrafficSecretProtectsTheTraceEarlyDataRecord()
    {
        using Tls13ClientHandshake client = ResumedClient(RecordedSimpleSession());
        byte[] secret = client.Start().SecretsInstalled.Single().Secret;

        using Tls13RecordProtection protection = Tls13RecordProtection.Create(client.EarlyDataCipherSuite!, secret);

        AssertHex("1703030017ab1df420e75c457a7cc5d2844f76d5aee4b4edbf049be0", protection.Protect(TlsContentType.ApplicationData, "ABCDEF"u8));
    }

    [TestMethod]
    public void ResumedHandshakeInstallsTheTraceHandshakeSecretsOnTheServerHello()
    {
        using Tls13ClientHandshake client = ResumedClient(RecordedSimpleSession());
        client.Start();

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, Hex(ResumedServerHello));

        Assert.IsNull(output.Failure);
        Assert.IsTrue(client.IsResumed);
        AssertSecret(output.SecretsInstalled[0], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Read, "fe927ae271312e8bf0275b581c54eef020450dc4ecffaa05a1a35d27518e7803");
        AssertSecret(output.SecretsInstalled[1], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Write, "2faac08f851d35fea3604fcb4de82dc62c9b164a70974d0462e27f1ab278700f");
    }

    [TestMethod]
    public void ResumedHandshakeAcceptsTheEarlyDataAndSendsTheTraceEndOfEarlyDataAndFinished()
    {
        using Tls13ClientHandshake client = ResumedClient(RecordedSimpleSession());
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Hex(ResumedServerHello));

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Handshake, Hex(ResumedEncryptedExtensions + ResumedServerFinished));

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.EarlyDataAccepted);
        Assert.HasCount(2, output.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.EarlyData, output.BytesToSend[0].Level);
        AssertHex(ResumedEndOfEarlyData, output.BytesToSend[0].Bytes);
        Assert.AreEqual(TlsEncryptionLevel.Handshake, output.BytesToSend[1].Level);
        AssertHex(ResumedClientFinished, output.BytesToSend[1].Bytes);
        AssertSecret(output.SecretsInstalled[0], TlsEncryptionLevel.Application, TlsTrafficDirection.Read, "cc21f1bf8feb7dd5fa505bd9c4b468a9984d554a993dc49e6d285598fb672691");
        AssertSecret(output.SecretsInstalled[1], TlsEncryptionLevel.Application, TlsTrafficDirection.Write, "2abbf2b8e381d23dbebe1dd2a7d16a8bf484cb4950d23fb7fb7fa8547062d9a1");
        AssertHex("5e95bdf1f89005ea2e9aa0ba85e728e3c19c5fe0c699e3f5bee59faebd0b5406", client.ResumptionMasterSecret!);
        Assert.IsEmpty(client.ServerCertificates);
    }

    [TestMethod]
    public void ResumedHandshakeWithoutEndOfEarlyDataSendsOnlyTheFinished()
    {
        using Tls13ClientHandshake client = ResumedClient(RecordedSimpleSession(), settings => settings with { SendEndOfEarlyData = false });
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Hex(ResumedServerHello));

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Handshake, Hex(ResumedEncryptedExtensions + ResumedServerFinished));

        Assert.IsTrue(client.EarlyDataAccepted);
        Assert.AreEqual(TlsEncryptionLevel.Handshake, output.BytesToSend.Single().Level);
        Diagnostics.Act("early data accepted", client.EarlyDataAccepted);
        Diagnostics.Act("flights sent", output.BytesToSend.Count);
        Diagnostics.Diff("Finished, which must differ from the trace's", Hex(ResumedClientFinished), output.BytesToSend[^1].Bytes);
        Assert.IsFalse(output.BytesToSend.Single().Bytes.AsSpan().SequenceEqual(Hex(ResumedClientFinished)), "The Finished covers a transcript without EndOfEarlyData.");
    }

    [TestMethod]
    public void ResumedSessionIsExportedAndReadBackWithTheTraceTicket()
    {
        TlsSessionRecord session = RecordedSimpleSession();

        TlsSessionRecord decoded = TlsSessionCodec.Decode(TlsSessionCodec.Encode(session))!;

        using Tls13ClientHandshake client = ResumedClient(decoded);
        AssertHex(ResumedClientHello, client.Start().BytesToSend.Single().Bytes);
    }

    /// <summary>Runs RFC 8448 section 3 with the ticket arriving at <see cref="TicketReceived" /> and returns the session it records.</summary>
    internal static TlsSessionRecord SimpleSession()
    {
        Tls13ClientSettings settings = TraceSettings() with
        {
            ExtensionOrder =
            [
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.SessionTicket,
                TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms,
                TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit,
            ],
            FixedExtensions = [RenegotiationInfo, SessionTicket, PskKeyExchangeModes, RecordSizeLimit],
            TimeProvider = new FixedTimeProvider(TicketReceived),
        };
        ReplayTlsRandomSource random = new([Hex(SimpleClientRandom)], [new X25519KeyShare(Hex(SimpleClientPrivateKey))]);
        using Tls13ClientHandshake client = new(settings, random, new RecordingCertificateVerifier());
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Hex(SimpleServerHello));
        client.Receive(TlsEncryptionLevel.Handshake, Hex(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished));
        Assert.IsNull(client.Receive(TlsEncryptionLevel.Application, Hex(SimpleNewSessionTicket)).Failure);
        return client.ReceivedSessions.Single();
    }

    // Runs SimpleSession under a PHASE and writes the trace inputs it replays and the
    // session it records.
    private TlsSessionRecord RecordedSimpleSession()
    {
        Diagnostics.Arrange(nameof(SimpleClientRandom), SimpleClientRandom);
        Diagnostics.Arrange(nameof(SimpleClientPrivateKey), SimpleClientPrivateKey);
        Diagnostics.Arrange("ticket received at", TicketReceived);

        TlsSessionRecord session;
        using (Diagnostics.Phase("section 3 handshake"))
        {
            session = SimpleSession();
        }

        Diagnostics.Act("session cipher suite", $"0x{session.CipherSuite:x4}");
        Diagnostics.Bytes("session ticket", session.Ticket);
        return session;
    }

    private static Tls13ClientSettings TraceSettings() => new()
    {
        ServerName = "server",
        CipherSuites = [0x1301, 0x1303, 0x1302],
        SupportedGroups = TraceGroups,
        KeyShareGroups = [TlsNamedGroup.X25519],
        SignatureAlgorithms = TraceSignatureAlgorithms,
    };

    private Tls13ClientHandshake ResumedClient(TlsSessionRecord session, Func<Tls13ClientSettings, Tls13ClientSettings>? change = null)
    {
        Tls13ClientSettings settings = TraceSettings() with
        {
            ExtensionOrder =
            [
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.KeyShare,
                TlsExtensionType.EarlyData, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms,
                TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit, TlsExtensionType.Padding,
            ],
            FixedExtensions = [RenegotiationInfo, PskKeyExchangeModes, RecordSizeLimit],
            TimeProvider = new FixedTimeProvider(session.ReceivedAt.AddMilliseconds(6)),
            ResumptionSession = session,
            OfferEarlyData = true,
        };
        ReplayTlsRandomSource random = new([Diagnostics.ArrangeHex(nameof(ResumedClientRandom), ResumedClientRandom)], [new X25519KeyShare(Diagnostics.ArrangeHex(nameof(ResumedClientPrivateKey), ResumedClientPrivateKey))]);
        return new Tls13ClientHandshake((change ?? (same => same))(settings), random, new RecordingCertificateVerifier());
    }

    private void AssertSecret(Tls13TrafficSecret secret, TlsEncryptionLevel level, TlsTrafficDirection direction, string expected, [CallerArgumentExpression(nameof(secret))] string label = "")
    {
        Assert.AreEqual(level, secret.Level);
        Assert.AreEqual(direction, secret.Direction);
        AssertHex(expected, secret.Secret, label);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private void AssertHex(string expected, byte[] actual, [CallerArgumentExpression(nameof(actual))] string label = "") =>
        Assert.AreEqual(expected, Diagnostics.ActAndDiffHex(label, expected, actual));
}
