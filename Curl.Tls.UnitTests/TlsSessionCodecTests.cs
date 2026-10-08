using System.Formats.Asn1;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Pins <see cref="TlsSessionCodec" /> to OpenSSL's <c>SSL_SESSION_ASN1</c> layout: the
/// untagged fields in order, the explicit context tags, zero integers left out, and a
/// reader that refuses anything that is not a version 1 session with a ticket.
/// </summary>
[TestClass]
public sealed class TlsSessionCodecTests
{
    private static readonly DateTimeOffset Received = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static readonly TlsSessionRecord Small = new(0x0304, 0x1301, [1, 2], [3, 4], [5, 6], 7200, 0x01020304, 0, Received)
    {
        ServerName = "a",
        Group = TlsNamedGroup.X25519,
    };

    // SEQUENCE { 1, 0x0304, "1301", "0102", "0304", [1] 1700000000, [2] 7200, [4] "",
    // [6] "a", [9] 7200, [10] "0506", [14] 0x01020304, [19] 29 }
    private const string SmallDer =
        "3043" + "020101" + "02020304" + "04021301" + "04020102" + "04020304" + "a10602046553f100" + "a20402021c20" + "a4020400"
        + "a603040161" + "a90402021c20" + "aa0404020506" + "ae06020401020304" + "b30302011d";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EncodeWritesOpenSslsLayoutAndLeavesOutZeroesAndAbsentFields()
    {
        Diagnostics.Arrange("session", "TLS 1.3, suite 0x1301, server name a, X25519, lifetime 7200");

        string encoded = Convert.ToHexStringLower(TlsSessionCodec.Encode(Small));
        Diagnostics.Act("encoded length", encoded.Length / 2);

        Diagnostics.Diff("encoded hex", SmallDer, encoded);
        Assert.AreEqual(SmallDer, encoded);
    }

    [TestMethod]
    public void DecodeReadsOpenSslsLayout()
    {
        Diagnostics.Arrange("der", "the encoding of the small session");

        TlsSessionRecord session = Decode(Convert.FromHexString(SmallDer))!;

        Diagnostics.Assert("version", 0x0304, session.Version);
        Diagnostics.Assert("server name", "a", session.ServerName);
        Assert.AreEqual(0x0304, session.Version);
        Assert.AreEqual(0x1301, session.CipherSuite);
        CollectionAssert.AreEqual(Small.SessionId, session.SessionId);
        CollectionAssert.AreEqual(Small.PreSharedKey, session.PreSharedKey);
        CollectionAssert.AreEqual(Small.Ticket, session.Ticket);
        Assert.AreEqual(7200u, session.TicketLifetime);
        Assert.AreEqual(0x01020304u, session.TicketAgeAdd);
        Assert.AreEqual(0u, session.MaxEarlyDataSize);
        Assert.AreEqual(Received, session.ReceivedAt);
        Assert.AreEqual("a", session.ServerName);
        Assert.IsNull(session.ApplicationProtocol);
        Assert.AreEqual(TlsNamedGroup.X25519, session.Group);
        Assert.IsNull(session.PeerCertificate);
    }

    [TestMethod]
    public void EveryFieldSurvivesARoundTrip()
    {
        byte[] certificate = TestServerCredential.Ed25519().Certificate;
        TlsSessionRecord session = Small with
        {
            MaxEarlyDataSize = 16384,
            ApplicationProtocol = "h2",
            PeerCertificate = certificate,
            ServerName = null,
        };
        Diagnostics.Arrange("session", "max early data 16384, ALPN h2, an Ed25519 peer certificate, no server name");

        TlsSessionRecord decoded = Decode(TlsSessionCodec.Encode(session))!;

        Diagnostics.Assert("max early data size", 16384u, decoded.MaxEarlyDataSize);
        Diagnostics.Assert("application protocol", "h2", decoded.ApplicationProtocol);
        Diagnostics.Diff("peer certificate", certificate, decoded.PeerCertificate ?? []);
        Assert.AreEqual(16384u, decoded.MaxEarlyDataSize);
        Assert.AreEqual("h2", decoded.ApplicationProtocol);
        Assert.IsNull(decoded.ServerName);
        CollectionAssert.AreEqual(certificate, decoded.PeerCertificate);
    }

    [TestMethod]
    public void DecodeSkipsTheFieldsATls13ClientDoesNotUse()
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        WriteExplicit(writer, 0, inner => inner.WriteOctetString([9]));
        WriteExplicit(writer, 5, inner => inner.WriteInteger(20));
        WriteExplicit(writer, 10, inner => inner.WriteOctetString([7]));
        WriteExplicit(writer, 13, inner => inner.WriteInteger(1));
        WriteExplicit(writer, 20, inner => inner.WriteOctetString([8]));
        writer.PopSequence();
        Diagnostics.Arrange("fields", "[0] cipher, [5] timeout, [10] ticket, [13] extended master secret, [20] unused");

        TlsSessionRecord session = Decode(writer.Encode())!;

        Diagnostics.Diff("ticket", new byte[] { 7 }, session.Ticket);
        Diagnostics.Assert("received at", DateTimeOffset.UnixEpoch, session.ReceivedAt);
        CollectionAssert.AreEqual(new byte[] { 7 }, session.Ticket);
        Assert.AreEqual(0, session.Group);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, session.ReceivedAt);
    }

    [TestMethod]
    [DataRow("", DisplayName = "no bytes")]
    [DataRow("010203", DisplayName = "not a sequence")]
    [DataRow("300302010200", DisplayName = "bytes after the sequence")]
    public void DecodeRefusesWhatIsNotASession(string hex)
    {
        Diagnostics.Arrange("hex", hex);

        TlsSessionRecord? session = Decode(Convert.FromHexString(hex));

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesAnotherFormatVersion()
    {
        Diagnostics.Arrange("format version", 2);

        TlsSessionRecord? session = Decode(WithTicket(Header(2, 0x0304, [0x13, 0x01])));

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesACipherThatIsNotTwoBytes()
    {
        Diagnostics.Arrange("cipher", "130100");

        TlsSessionRecord? session = Decode(WithTicket(Header(1, 0x0304, [0x13, 0x01, 0x00])));

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesAVersionPastSixteenBits()
    {
        Diagnostics.Arrange("version", "0x10000");

        TlsSessionRecord? session = Decode(WithTicket(Header(1, 0x10000, [0x13, 0x01])));

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesASessionWithoutATicket()
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        writer.PopSequence();
        Diagnostics.Arrange("fields", "the untagged fields only, no [10] ticket");

        TlsSessionRecord? session = Decode(writer.Encode());

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    [DataRow(10, 9, DisplayName = "out of order")]
    [DataRow(10, 10, DisplayName = "repeated")]
    public void DecodeRefusesFieldsOutOfOrder(int first, int second)
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        WriteExplicit(writer, first, inner => inner.WriteOctetString([7]));
        WriteExplicit(writer, second, inner => inner.WriteOctetString([7]));
        writer.PopSequence();
        Diagnostics.Arrange("tags", $"[{first}] then [{second}]");

        TlsSessionRecord? session = Decode(writer.Encode());

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesAnUntaggedFieldAfterTheMasterKey()
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        writer.WriteInteger(5);
        writer.PopSequence();
        Diagnostics.Arrange("extra field", "untagged INTEGER 5 after the master key");

        TlsSessionRecord? session = Decode(writer.Encode());

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesATimeBefore1970()
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        WriteExplicit(writer, 1, inner => inner.WriteInteger(-1));
        Diagnostics.Arrange("[1] time", -1);

        TlsSessionRecord? session = Decode(WithTicket(writer));

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void DecodeRefusesAGroupPastSixteenBits()
    {
        AsnWriter writer = Header(1, 0x0304, [0x13, 0x01]);
        WriteExplicit(writer, 10, inner => inner.WriteOctetString([7]));
        WriteExplicit(writer, 19, inner => inner.WriteInteger(70000));
        writer.PopSequence();
        Diagnostics.Arrange("[19] group", 70000);

        TlsSessionRecord? session = Decode(writer.Encode());

        WriteRefused(session);
        Assert.IsNull(session);
    }

    [TestMethod]
    public void EncodeAndDecodeRefuseNull()
    {
        Diagnostics.Arrange("argument", "null");

        ArgumentNullException encodeException = Assert.ThrowsExactly<ArgumentNullException>(() => TlsSessionCodec.Encode(null!));
        ArgumentNullException decodeException = Assert.ThrowsExactly<ArgumentNullException>(() => TlsSessionCodec.Decode(null!));
        Diagnostics.Act("thrown", $"{encodeException.GetType().Name}, {decodeException.GetType().Name}");

        Diagnostics.Assert("parameter names", "session, encoded", $"{encodeException.ParamName}, {decodeException.ParamName}");
    }

    /// <summary>Decodes <paramref name="der" />, writing the bytes and whether a session came back.</summary>
    private TlsSessionRecord? Decode(byte[] der)
    {
        Diagnostics.Bytes("der", der);
        TlsSessionRecord? session = TlsSessionCodec.Decode(der);
        Diagnostics.Act("decoded", session is null ? "null" : "a session");
        return session;
    }

    private void WriteRefused(TlsSessionRecord? session) => Diagnostics.Assert("refused", true, session is null);

    /// <summary>Opens the session sequence and writes the untagged fields, leaving the sequence open.</summary>
    private static AsnWriter Header(int formatVersion, int version, byte[] cipher)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteInteger(formatVersion);
        writer.WriteInteger(version);
        writer.WriteOctetString(cipher);
        writer.WriteOctetString([1]);
        writer.WriteOctetString([2]);
        return writer;
    }

    private static byte[] WithTicket(AsnWriter writer)
    {
        WriteExplicit(writer, 10, inner => inner.WriteOctetString([7]));
        writer.PopSequence();
        return writer.Encode();
    }

    private static void WriteExplicit(AsnWriter writer, int tag, Action<AsnWriter> write)
    {
        Asn1Tag explicitTag = new(TagClass.ContextSpecific, tag, true);
        writer.PushSequence(explicitTag);
        write(writer);
        writer.PopSequence(explicitTag);
    }
}
