using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Pins <see cref="HpackEncoder" /> to RFC 7541 appendix C.4 and to the header block real
/// curl sent, and checks each representation choice ADR-0148 takes from nghttp2.
/// </summary>
[TestClass]
public sealed class HpackEncoderTests
{
    /// <summary>
    /// The HEADERS frame payload curl 8.18.0 (nghttp2 1.68.0, curl.se's Windows build) sent
    /// for <c>curl --http2-prior-knowledge -H "Authorization: Basic dXNlcjpwYXNz"
    /// -H "Cookie: a=b" -H "X-Custom: hello-world-value" -H "X-Custom: hello-world-value"
    /// -A curl/8.18.0 http://127.0.0.1:48656/some/path?q=1</c>, recorded with
    /// Record-CurlExchange.ps1 on 2026-09-28.
    /// </summary>
    private const string CurlHeaderBlock =
        "8286418b089d5c0b8170dc69e71b73048b6107a4ac5634cffe76803f7a8825b650c3cb85e5c153032a2f2a"
        + "1f088fba34188a49f9a68274afc73fcd3eff1f1103613d624086f2b12d424f4f8d9cb45075bc1eca245bb8e8b4bfbe";

    // RFC 7541 appendix C.4.1, C.4.2 and C.4.3, on one encoder.
    [TestMethod]
    public void Encode_C4Requests_GiveThePublishedBlocks()
    {
        var encoder = new HpackEncoder();

        CollectionAssert.AreEqual(
            FromHex("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff"),
            encoder.Encode(Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com"))));
        CollectionAssert.AreEqual(
            FromHex("8286 84be 5886 a8eb 1064 9cbf"),
            encoder.Encode(Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com"), ("cache-control", "no-cache"))));
        CollectionAssert.AreEqual(
            FromHex("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf"),
            encoder.Encode(Fields((":method", "GET"), (":scheme", "https"), (":path", "/index.html"), (":authority", "www.example.com"), ("custom-key", "custom-value"))));
        Assert.AreEqual(164, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_CurlsRequestHeaders_GivesTheBlockCurlSent()
    {
        var block = new HpackEncoder().Encode(Fields(
            (":method", "GET"),
            (":scheme", "http"),
            (":authority", "127.0.0.1:48656"),
            (":path", "/some/path?q=1"),
            ("user-agent", "curl/8.18.0"),
            ("accept", "*/*"),
            ("authorization", "Basic dXNlcjpwYXNz"),
            ("cookie", "a=b"),
            ("x-custom", "hello-world-value"),
            ("x-custom", "hello-world-value")));

        CollectionAssert.AreEqual(Convert.FromHexString(CurlHeaderBlock), block);
    }

    [TestMethod]
    public void Encode_NotIndexedName_IsALiteralWithoutIndexing()
    {
        var encoder = new HpackEncoder();

        CollectionAssert.AreEqual(FromHex("0f0d 0135"), encoder.Encode(Fields(("content-length", "5"))));
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_FieldBiggerThanThreeQuartersOfTheTable_IsALiteralWithoutIndexing()
    {
        var encoder = new HpackEncoder(128);

        var block = encoder.Encode(Fields(("x", new string('z', 64))));

        Assert.AreEqual(0x00, block[0]);
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_FieldMarkedNeverIndexed_IsALiteralNeverIndexedEvenWhenTheStaticTableHasIt()
    {
        var block = new HpackEncoder().Encode([new HeaderField(":method", "GET", IsNeverIndexed: true)]);

        CollectionAssert.AreEqual(FromHex("12 03474554"), block);
    }

    [TestMethod]
    public void Encode_AuthorizationAlreadyInTheTable_IsStillALiteralNeverIndexed()
    {
        var encoder = new HpackEncoder();

        var first = encoder.Encode(Fields(("proxy-authorization", "x")));
        var second = encoder.Encode(Fields(("proxy-authorization", "x")));

        CollectionAssert.AreEqual(FromHex("1f22 0178"), first);
        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void Encode_CookieOfTwentyBytes_IsIndexed()
    {
        var encoder = new HpackEncoder();

        var block = encoder.Encode(Fields(("cookie", "abcdefghij0123456789")));

        Assert.AreEqual(0x60, block[0]);
        Assert.AreEqual(58, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_NameOnlyInTheDynamicTable_NamesTheNewestEntry()
    {
        var encoder = new HpackEncoder();
        encoder.Encode(Fields(("x-a", "1"), ("x-a", "2")));

        var block = encoder.Encode(Fields(("x-a", "3")));

        CollectionAssert.AreEqual(FromHex("7e 0133"), block);
    }

    [TestMethod]
    public void Encode_NeverIndexedFieldWhoseValueIsInTheDynamicTable_NamesTheEntryButSendsTheValue()
    {
        var encoder = new HpackEncoder();
        encoder.Encode(Fields(("x-a", "1")));

        var block = encoder.Encode([new HeaderField("x-a", "1", IsNeverIndexed: true)]);

        CollectionAssert.AreEqual(FromHex("1f2f 0131"), block);
    }

    [TestMethod]
    public void Encode_ValueAlreadyInTheDynamicTable_IsItsIndex()
    {
        var encoder = new HpackEncoder();
        encoder.Encode(Fields(("x-a", "1"), ("x-a", "2")));

        CollectionAssert.AreEqual(FromHex("bf"), encoder.Encode(Fields(("x-a", "1"))));
    }

    [TestMethod]
    public void Encode_AfterPeerLowersThenRaisesTheTable_OpensWithBothUpdates()
    {
        var encoder = new HpackEncoder();
        encoder.Encode(Fields(("x-a", "1")));
        encoder.SetPeerMaximumTableSize(0);
        encoder.SetPeerMaximumTableSize(4096);

        CollectionAssert.AreEqual(FromHex("20 3fe11f 82"), encoder.Encode(Fields((":method", "GET"))));
        CollectionAssert.AreEqual(FromHex("82"), encoder.Encode(Fields((":method", "GET"))));
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_PeerAllowsMoreThanTheEncodersLimit_UpdatesToTheLimitOnly()
    {
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize(65536);

        CollectionAssert.AreEqual(FromHex("3fe11f"), encoder.Encode([]));
        Assert.AreEqual(4096, encoder.MaximumTableSize);
    }

    [TestMethod]
    public void Encode_PeerAllowsNoTable_IndexesNothing()
    {
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize(0);

        CollectionAssert.AreEqual(FromHex("20 0003782d61 0131"), encoder.Encode(Fields(("x-a", "1"))));
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HpackEncoder().Encode(null!));
    }

    [TestMethod]
    public void Constructor_NegativeTableSize_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder(-1));
    }

    [TestMethod]
    public void SetPeerMaximumTableSize_Negative_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder().SetPeerMaximumTableSize(-1));
    }
}
