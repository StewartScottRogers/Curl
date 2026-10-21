using System.Security.Cryptography;
using System.Text;

using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

[TestClass]
public sealed class TlsSessionCacheTests
{
    private const string PeerKey = "example.com:443:IMPL-Curl:G";

    private static readonly DateTimeOffset Received = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static readonly byte[] Salt = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void PeerKey_NamesHostPortVerificationCaFileAndImplementation()
    {
        Diagnostics.Arrange("peers", "example.com:443 default; h:8443 insecure with status; h:1 with ca.pem; h:1 with null options");

        var plain = TlsSessionCache.PeerKey("example.com", 443, new TlsClientOptions());
        var insecure = TlsSessionCache.PeerKey("h", 8443, new TlsClientOptions(Insecure: true, CaCertificateFile: "ca.pem", RequireCertificateStatus: true));
        Diagnostics.Act("plain key", plain);
        Diagnostics.Act("insecure key", insecure);
        Diagnostics.Assert("plain key", "example.com:443:IMPL-Curl:G", plain);
        Diagnostics.Assert("insecure key", "h:8443:NO-VRFY-PEER:NO-VRFY-HOST:VRFY-STATUS:IMPL-Curl:G", insecure);

        Assert.AreEqual("example.com:443:IMPL-Curl:G", TlsSessionCache.PeerKey("example.com", 443, new TlsClientOptions()));
        Assert.AreEqual("h:8443:NO-VRFY-PEER:NO-VRFY-HOST:VRFY-STATUS:IMPL-Curl:G", TlsSessionCache.PeerKey("h", 8443, new TlsClientOptions(Insecure: true, CaCertificateFile: "ca.pem", RequireCertificateStatus: true)));
        Assert.AreEqual($"h:1:CA-{Path.GetFullPath("ca.pem")}:IMPL-Curl:G", TlsSessionCache.PeerKey("h", 1, new TlsClientOptions(CaCertificateFile: "ca.pem")));
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => TlsSessionCache.PeerKey("h", 1, null!));
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void PeerKey_ClientCertificateKeyAndTypes_NamedUpperCasedAndWithoutThePassPhrase()
    {
        var options = new TlsClientOptions(ClientCertificate: "client.pem", PrivateKey: "client.key", CertificateType: "pem", PrivateKeyType: "Der", Passphrase: "secret");
        Diagnostics.Arrange("options", options);

        var key = TlsSessionCache.PeerKey("h", 1, options);

        Diagnostics.Act("key", key);
        Assert.AreEqual($"h:1:CCERT-{Path.GetFullPath("client.pem")}:KEY-{Path.GetFullPath("client.key")}:CT-PEM:KT-DER:IMPL-Curl:G", key);
        Assert.AreEqual(key, TlsSessionCache.PeerKey("h", 1, options with { Passphrase = null }), "the pass phrase is not part of the key (upstream test3304)");
        Assert.AreEqual("h:1:IMPL-Curl:G", TlsSessionCache.PeerKey("h", 1, new TlsClientOptions(ClientCertificate: "", PrivateKey: "", CertificateType: "", PrivateKeyType: "")), "empty values add nothing");
    }

    [TestMethod]
    public void Constructor_WithoutClock_Throws()
    {
        Diagnostics.Arrange("time provider", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSessionCache(null!));

        Diagnostics.Act("parameter name", exception.ParamName);
        Diagnostics.Assert("parameter name", "timeProvider", exception.ParamName);

        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSessionCache(null!)).ParamName);
    }

    [TestMethod]
    public void NewRandomSalt_Called_Returns32Bytes()
    {
        Diagnostics.Arrange("call", "NewRandomSalt()");

        var salt = TlsSessionCache.NewRandomSalt();

        Diagnostics.Act("salt length", salt.Length);
        Diagnostics.Assert("salt length", 32, salt.Length);

        Assert.HasCount(32, TlsSessionCache.NewRandomSalt());
    }

    [TestMethod]
    public void Export_WithNoSession_IsEmpty()
    {
        Diagnostics.Arrange("cache", "no tracked or imported session");

        var exported = new TlsSessionCache(new FixedClock(Received)).Export();

        Diagnostics.Act("exported length", exported.Length);
        Diagnostics.Assert("exported", string.Empty, exported);

        Assert.AreEqual(string.Empty, new TlsSessionCache(new FixedClock(Received)).Export());
    }

    // The file bytes for a fixed session: curl's two comment lines, then the salt and the
    // HMAC-SHA256 of the peer key under it, and the packed SSL_SESSION.
    [TestMethod]
    public void Export_TrackedSession_WritesCurlsLine()
    {
        var cache = new TlsSessionCache(new FixedClock(Received), () => Salt);
        var session = Session(maxEarlyData: 16384);
        Diagnostics.Arrange("peer key", PeerKey);
        Diagnostics.Arrange("max early data", 16384);
        cache.Track(PeerKey, () => [session]);

        string text;
        using (Diagnostics.Phase("export"))
        {
            text = cache.Export();
        }

        Diagnostics.Act("exported lines", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Diagnostics.Act("exported text", text);

        byte[] shmac = [.. Salt, .. HMACSHA256.HashData(Salt, Encoding.ASCII.GetBytes(PeerKey))];
        byte[] packed = TlsSessionPacking.Pack(new PackedTlsSession(TlsSessionCodec.Encode(session), 0x0304, 1_800_007_200, "http/1.1", 16384, null));
        Diagnostics.Bytes("shmac", shmac);
        Diagnostics.Bytes("packed", packed);
        Diagnostics.Assert(
            "exported text",
            "# Your SSL session cache. https://curl.se/docs/ssl-sessions.html\n# This file was generated by libcurl! Edit at your own risk.\n"
                + $"{Convert.ToBase64String(shmac)}:{Convert.ToBase64String(packed)}\n",
            text);
        Assert.AreEqual(
            "# Your SSL session cache. https://curl.se/docs/ssl-sessions.html\n# This file was generated by libcurl! Edit at your own risk.\n"
                + $"{Convert.ToBase64String(shmac)}:{Convert.ToBase64String(packed)}\n",
            text);
        Assert.AreEqual(text, cache.Export(), "a tracked connection is read once and its peer keeps its salt");
    }

    [TestMethod]
    public void Import_ExportedFile_ResumesTheSessionForTheSamePeerOnly()
    {
        var writer = new TlsSessionCache(new FixedClock(Received), () => Salt);
        writer.Track(PeerKey, () => [Session()]);
        var text = writer.Export();
        Diagnostics.Arrange("peer key", PeerKey);
        Diagnostics.Arrange("exported lines", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        var reader = new TlsSessionCache(new FixedClock(Received.AddSeconds(10)));
        using (Diagnostics.Phase("import"))
        {
            Assert.AreEqual(0, reader.Import(text.Replace("\n", "\r\n", StringComparison.Ordinal) + "\n  \n", "f").Count);
        }

        Diagnostics.Act("other peer", reader.Take("other:443:IMPL-Curl:G") is null ? "null" : "session");
        Assert.IsNull(reader.Take("other:443:IMPL-Curl:G"));
        var resumed = reader.Take(PeerKey);
        Diagnostics.Act("same peer", resumed is null ? "null" : "session");
        Diagnostics.Assert("same peer", "session", resumed is null ? "null" : "session");
        Assert.IsNotNull(resumed);
        Diagnostics.Bytes("resumed ticket", resumed.Ticket);
        CollectionAssert.AreEqual(Session().Ticket, resumed.Ticket);
        Assert.IsNull(reader.Take(PeerKey), "a TLS 1.3 session is taken out when offered");
        Diagnostics.Act("export after take", reader.Export().Length);
        Diagnostics.Assert("export after take", string.Empty, reader.Export());
        Assert.AreEqual(string.Empty, reader.Export());
    }

    [TestMethod]
    public void Import_UnusedLine_IsWrittenBackUnchanged()
    {
        var writer = new TlsSessionCache(new FixedClock(Received), () => Salt);
        writer.Track(PeerKey, () => [Session()]);
        var text = writer.Export();
        var reader = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Arrange("imported text", text);

        using (Diagnostics.Phase("import"))
        {
            reader.Import(text, "f");
        }

        var written = reader.Export();
        Diagnostics.Act("exported text", written);
        Diagnostics.Assert("exported text", text, written);

        Assert.AreEqual(text, reader.Export());
    }

    [TestMethod]
    public void Import_BadLines_WarnsAsCurlDoes()
    {
        var shmac = Convert.ToBase64String(new byte[64]);
        var packed = Convert.ToBase64String(TlsSessionPacking.Pack(new PackedTlsSession([1], 0x0304, 1, null, 0, null)));
        var text = string.Join('\n',
            "# comment: with a colon",
            "no colon here",
            "QQ=:" + packed,
            shmac + ":Q Q=",
            shmac + ":" + Convert.ToBase64String([9]),
            Convert.ToBase64String(new byte[10]) + ":" + packed,
            "QUJD===:" + packed,
            ":" + packed,
            "QU*D:" + packed);
        Diagnostics.Arrange("file lines", text.Split('\n').Length);

        var warnings = new TlsSessionCache(new FixedClock(Received)).Import(text, "s.txt");

        Diagnostics.Act("warnings", string.Join(" | ", warnings));
        Diagnostics.Assert("warning count", 8, warnings.Count);

        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: unrecognized line 1 in SSL session file s.txt",
                "Warning: invalid shmax base64 encoding in line 2",
                "Warning: invalid sdata base64 encoding in line 3: Q Q=",
                "Warning: import of session from line 4 rejected(26)",
                "Warning: import of session from line 5 rejected(43)",
                "Warning: invalid shmax base64 encoding in line 6",
                "Warning: invalid shmax base64 encoding in line 7",
                "Warning: invalid shmax base64 encoding in line 8",
            },
            warnings.ToArray());
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSessionCache(new FixedClock(Received)).Import(null!, "f"));
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    // Sessions not from TLS 1.3, or whose bytes are not an SSL_SESSION, are never offered;
    // one not from TLS 1.3 replaces the others, and a TLS 1.3 one replaces those.
    [TestMethod]
    public void Take_SkipsSessionsItCannotResume()
    {
        var shmac = Convert.ToBase64String([.. Salt, .. HMACSHA256.HashData(Salt, Encoding.ASCII.GetBytes(PeerKey))]);
        string Line(PackedTlsSession session) => shmac + ":" + Convert.ToBase64String(TlsSessionPacking.Pack(session));
        var good = new PackedTlsSession(TlsSessionCodec.Encode(Session()), 0x0304, Received.ToUnixTimeSeconds() + 100, null, 0, null);
        var cache = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Arrange("peer key", PeerKey);
        Diagnostics.Arrange("first import", "a TLS 1.3 line then a TLS 1.2 line");

        cache.Import(string.Join('\n', Line(good), Line(good with { ProtocolId = 0x0303 })), "f");
        Diagnostics.Assert("take after TLS 1.2 replaced", "null", "checked by the next assertion");
        Assert.IsNull(cache.Take(PeerKey), "the TLS 1.2 session replaced the TLS 1.3 one and is not offered");

        Diagnostics.Arrange("second import", "a TLS 1.2 line, a TLS 1.3 line, an undecodable line");
        cache.Import(string.Join('\n', Line(good with { ProtocolId = 0x0303 }), Line(good), Line(good with { SessionData = [1, 2] })), "f");
        Diagnostics.Act("exported lines after second import", cache.Export().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Diagnostics.Assert("take after second import", "a session", "checked by the next assertion");
        Assert.IsNotNull(cache.Take(PeerKey), "the undecodable newest is dropped, the one before it offered");
    }

    [TestMethod]
    public void Track_KeepsTheNewestTwoUnexpiredSessionsPerPeer()
    {
        var clock = new FixedClock(Received);
        var cache = new TlsSessionCache(clock, () => Salt);
        var sessions = Enumerable.Range(1, 3).Select(value => Session(ticket: (byte)value)).ToArray();
        Diagnostics.Arrange("sessions tracked", "three for the peer, one short-lived (10 s) for another");
        cache.Track(PeerKey, () => sessions);
        cache.Track("short:443:IMPL-Curl:G", () => [Session(lifetime: 10)]);

        clock.Now = Received.AddSeconds(60);
        var lines = cache.Export().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();

        Diagnostics.Act("session lines after 60 s", lines.Length);
        Diagnostics.Assert("session lines after 60 s", 2, lines.Length);

        Assert.HasCount(2, lines);
        Diagnostics.Assert("newest ticket taken first", 3, "checked by the next assertion");
        Assert.AreEqual(3, cache.Take(PeerKey)!.Ticket[0]);
        Assert.AreEqual(2, cache.Take(PeerKey)!.Ticket[0]);
        Assert.IsNull(cache.Take(PeerKey), "the oldest of three was dropped");
    }

    [TestMethod]
    public void ImportSession_UnderPeerKey_IsOfferedToThatPeer()
    {
        var packed = TlsSessionPacking.Pack(new PackedTlsSession(TlsSessionCodec.Encode(Session()), 0x0304, Received.ToUnixTimeSeconds() + 100, null, 0, null));
        var cache = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Arrange("peer key", PeerKey);

        var code = cache.ImportSession(PeerKey, null, packed);

        Diagnostics.Act("code", code);
        Diagnostics.Assert("code", 0, code);
        Assert.AreEqual(0, code);
        Assert.IsNull(cache.Take("other:443:IMPL-Curl:G"));
        CollectionAssert.AreEqual(Session().Ticket, cache.Take(PeerKey)!.Ticket);
    }

    [TestMethod]
    public void ImportSession_UnderSaltAndHmac_IsOfferedToThePeerItNames()
    {
        byte[] shmac = [.. Salt, .. HMACSHA256.HashData(Salt, Encoding.ASCII.GetBytes(PeerKey))];
        var packed = TlsSessionPacking.Pack(new PackedTlsSession(TlsSessionCodec.Encode(Session()), 0x0304, Received.ToUnixTimeSeconds() + 100, null, 0, null));
        var cache = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Bytes("shmac", shmac);

        var code = cache.ImportSession(null, shmac, packed);

        Diagnostics.Act("code", code);
        Diagnostics.Assert("code", 0, code);
        Assert.AreEqual(0, code);
        Assert.IsNull(cache.Take("other:443:IMPL-Curl:G"));
        Assert.IsNotNull(cache.Take(PeerKey));
    }

    [TestMethod]
    public void ImportSession_Refused_AnswersCurlsCodes()
    {
        var packed = TlsSessionPacking.Pack(new PackedTlsSession([1], 0x0304, 1, null, 0, null));
        var cache = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Arrange("cases", "bad packing; neither key nor hmac; 10-byte hmac; null bytes");

        var badPacking = cache.ImportSession(PeerKey, null, [0x02]);
        var neither = cache.ImportSession(null, null, packed);
        var shortHmac = cache.ImportSession(null, new byte[10], packed);

        Diagnostics.Act("codes", $"{badPacking} {neither} {shortHmac}");
        Diagnostics.Assert("codes", "26 43 43", $"{badPacking} {neither} {shortHmac}");
        Assert.AreEqual(26, badPacking);
        Assert.AreEqual(43, neither);
        Assert.AreEqual(43, shortHmac);
        Assert.AreEqual(string.Empty, cache.Export());
        Assert.AreEqual("packedSession", Assert.ThrowsExactly<ArgumentNullException>(() => cache.ImportSession(PeerKey, null, null!)).ParamName);
    }

    // lib1678 imports one packet 64 times under example.test:443; every import answers OK.
    [TestMethod]
    public void ImportSession_Lib1678Packet64Times_AnswersOkEachTime()
    {
        byte[] packed =
        [
            0x01,
            0x04, 0x10, 0x00, .. new byte[4096],
            0x07, 0x00, 0x7F, .. new byte[127],
            0x05, 0x00, 0x0A, .. "abcdefghij"u8,
            0x04, 0x00, 0x01, 0xEE,
            0x05, 0x00, 0x01, (byte)'z',
            0x07, 0x00, 0x01, 0xDD,
        ];
        var cache = new TlsSessionCache(new FixedClock(Received));
        Diagnostics.Arrange("imports", 64);

        var codes = Enumerable.Range(0, 64).Select(_ => cache.ImportSession("example.test:443", null, packed)).ToArray();

        Diagnostics.Act("distinct codes", string.Join(',', codes.Distinct()));
        Diagnostics.Assert("distinct codes", "0", string.Join(',', codes.Distinct()));
        Assert.IsTrue(codes.All(code => code == 0));
    }

    [TestMethod]
    public void ListSessions_HeldSessions_ListsEachUnexpiredOneWithItsFields()
    {
        var cache = new TlsSessionCache(new FixedClock(Received), () => Salt);
        var session = Session(maxEarlyData: 16384);
        cache.Track(PeerKey, () => [session]);
        var expired = TlsSessionPacking.Pack(new PackedTlsSession([1], 0x0304, Received.ToUnixTimeSeconds() - 1, null, 0, null));
        cache.ImportSession("old:443:IMPL-Curl:G", null, expired);
        Diagnostics.Arrange("held", "one tracked TLS 1.3 session, one expired imported session");

        var held = cache.ListSessions();

        Diagnostics.Act("listed", held.Count);
        Diagnostics.Assert("listed", 1, held.Count);
        Assert.HasCount(1, held);
        var listed = held[0];
        Assert.AreEqual(PeerKey, listed.PeerKey);
        byte[] shmac = [.. Salt, .. HMACSHA256.HashData(Salt, Encoding.ASCII.GetBytes(PeerKey))];
        CollectionAssert.AreEqual(shmac, listed.SaltAndHmac);
        CollectionAssert.AreEqual(TlsSessionPacking.Pack(new PackedTlsSession(TlsSessionCodec.Encode(session), 0x0304, 1_800_007_200, "http/1.1", 16384, null)), listed.PackedSession);
        Assert.AreEqual(1_800_007_200, listed.ValidUntil);
        Assert.AreEqual(0x0304, listed.ProtocolId);
        Assert.AreEqual("http/1.1", listed.ApplicationProtocol);
        Assert.AreEqual(16384u, listed.MaxEarlyData);
    }

    private static TlsSessionRecord Session(uint maxEarlyData = 0, byte ticket = 7, uint lifetime = 7200) =>
        new(0x0304, 0x1301, new byte[32], new byte[32], [ticket, 1, 2], lifetime, 5, maxEarlyData, Received)
        {
            ServerName = "example.com",
            ApplicationProtocol = "http/1.1",
            Group = 0x001D,
        };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
