using Curl.Testing;
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

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 7541 appendix C.4.1, C.4.2 and C.4.3, on one encoder.
    [TestMethod]
    public void Encode_C4Requests_GiveThePublishedBlocks()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var firstFields = Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com"));
        var secondFields = Fields((":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com"), ("cache-control", "no-cache"));
        var thirdFields = Fields((":method", "GET"), (":scheme", "https"), (":path", "/index.html"), (":authority", "www.example.com"), ("custom-key", "custom-value"));
        diagnostics.Arrange("first fields", string.Join(", ", firstFields));
        diagnostics.Arrange("second fields", string.Join(", ", secondFields));
        diagnostics.Arrange("third fields", string.Join(", ", thirdFields));

        var firstBlock = encoder.Encode(firstFields);
        var secondBlock = encoder.Encode(secondFields);
        var thirdBlock = encoder.Encode(thirdFields);
        diagnostics.Bytes("first block", firstBlock);
        diagnostics.Bytes("second block", secondBlock);
        diagnostics.Bytes("third block", thirdBlock);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Diff("first block", FromHex("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff"), firstBlock);
        diagnostics.Diff("second block", FromHex("8286 84be 5886 a8eb 1064 9cbf"), secondBlock);
        diagnostics.Diff("third block", FromHex("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf"), thirdBlock);
        diagnostics.Assert("table size", 164, encoder.TableSize);
        CollectionAssert.AreEqual(FromHex("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff"), firstBlock);
        CollectionAssert.AreEqual(FromHex("8286 84be 5886 a8eb 1064 9cbf"), secondBlock);
        CollectionAssert.AreEqual(FromHex("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf"), thirdBlock);
        Assert.AreEqual(164, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_CurlsRequestHeaders_GivesTheBlockCurlSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fields = Fields(
            (":method", "GET"),
            (":scheme", "http"),
            (":authority", "127.0.0.1:48656"),
            (":path", "/some/path?q=1"),
            ("user-agent", "curl/8.18.0"),
            ("accept", "*/*"),
            ("authorization", "Basic dXNlcjpwYXNz"),
            ("cookie", "a=b"),
            ("x-custom", "hello-world-value"),
            ("x-custom", "hello-world-value"));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = new HpackEncoder().Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("encoded block length", block.Length);

        diagnostics.Diff("block", Convert.FromHexString(CurlHeaderBlock), block);
        CollectionAssert.AreEqual(Convert.FromHexString(CurlHeaderBlock), block);
    }

    [TestMethod]
    public void Encode_NotIndexedName_IsALiteralWithoutIndexing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var fields = Fields(("content-length", "5"));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Diff("block", FromHex("0f0d 0135"), block);
        diagnostics.Assert("table size", 0, encoder.TableSize);
        CollectionAssert.AreEqual(FromHex("0f0d 0135"), block);
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_FieldBiggerThanThreeQuartersOfTheTable_IsALiteralWithoutIndexing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder(128);
        var fields = Fields(("x", new string('z', 64)));
        diagnostics.Arrange("maximum table size", 128);
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Assert("first byte", 0x00, block[0]);
        diagnostics.Assert("table size", 0, encoder.TableSize);
        Assert.AreEqual(0x00, block[0]);
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_FieldMarkedNeverIndexed_IsALiteralNeverIndexedEvenWhenTheStaticTableHasIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var field = new HeaderField(":method", "GET", IsNeverIndexed: true);
        diagnostics.Arrange("field", field);

        var block = new HpackEncoder().Encode([field]);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("encoded block length", block.Length);

        diagnostics.Diff("block", FromHex("12 03474554"), block);
        CollectionAssert.AreEqual(FromHex("12 03474554"), block);
    }

    [TestMethod]
    public void Encode_AuthorizationAlreadyInTheTable_IsStillALiteralNeverIndexed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var fields = Fields(("proxy-authorization", "x"));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var first = encoder.Encode(fields);
        var second = encoder.Encode(Fields(("proxy-authorization", "x")));
        diagnostics.Bytes("first block", first);
        diagnostics.Bytes("second block", second);
        diagnostics.Act("second block length", second.Length);

        diagnostics.Diff("first block", FromHex("1f22 0178"), first);
        diagnostics.Diff("second block", first, second);
        CollectionAssert.AreEqual(FromHex("1f22 0178"), first);
        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void Encode_CookieOfTwentyBytes_IsIndexed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var fields = Fields(("cookie", "abcdefghij0123456789"));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Assert("first byte", 0x60, block[0]);
        diagnostics.Assert("table size", 58, encoder.TableSize);
        Assert.AreEqual(0x60, block[0]);
        Assert.AreEqual(58, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_NameOnlyInTheDynamicTable_NamesTheNewestEntry()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var seeded = Fields(("x-a", "1"), ("x-a", "2"));
        encoder.Encode(seeded);
        var fields = Fields(("x-a", "3"));
        diagnostics.Arrange("seeded fields", string.Join(", ", seeded));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("encoded block length", block.Length);

        diagnostics.Diff("block", FromHex("7e 0133"), block);
        CollectionAssert.AreEqual(FromHex("7e 0133"), block);
    }

    [TestMethod]
    public void Encode_NeverIndexedFieldWhoseValueIsInTheDynamicTable_NamesTheEntryButSendsTheValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var seeded = Fields(("x-a", "1"));
        encoder.Encode(seeded);
        var field = new HeaderField("x-a", "1", IsNeverIndexed: true);
        diagnostics.Arrange("seeded fields", string.Join(", ", seeded));
        diagnostics.Arrange("field", field);

        var block = encoder.Encode([field]);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("encoded block length", block.Length);

        diagnostics.Diff("block", FromHex("1f2f 0131"), block);
        CollectionAssert.AreEqual(FromHex("1f2f 0131"), block);
    }

    [TestMethod]
    public void Encode_ValueAlreadyInTheDynamicTable_IsItsIndex()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        var seeded = Fields(("x-a", "1"), ("x-a", "2"));
        encoder.Encode(seeded);
        var fields = Fields(("x-a", "1"));
        diagnostics.Arrange("seeded fields", string.Join(", ", seeded));
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("encoded block length", block.Length);

        diagnostics.Diff("block", FromHex("bf"), block);
        CollectionAssert.AreEqual(FromHex("bf"), block);
    }

    [TestMethod]
    public void Encode_AfterPeerLowersThenRaisesTheTable_OpensWithBothUpdates()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        encoder.Encode(Fields(("x-a", "1")));
        encoder.SetPeerMaximumTableSize(0);
        encoder.SetPeerMaximumTableSize(4096);
        var fields = Fields((":method", "GET"));
        diagnostics.Arrange("peer sizes", "0 then 4096");
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var first = encoder.Encode(fields);
        var second = encoder.Encode(fields);
        diagnostics.Bytes("first block", first);
        diagnostics.Bytes("second block", second);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Diff("first block", FromHex("20 3fe11f 82"), first);
        diagnostics.Diff("second block", FromHex("82"), second);
        diagnostics.Assert("table size", 0, encoder.TableSize);
        CollectionAssert.AreEqual(FromHex("20 3fe11f 82"), first);
        CollectionAssert.AreEqual(FromHex("82"), second);
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_PeerAllowsMoreThanTheEncodersLimit_UpdatesToTheLimitOnly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize(65536);
        diagnostics.Arrange("peer maximum table size", 65536);

        var block = encoder.Encode([]);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("maximum table size", encoder.MaximumTableSize);

        diagnostics.Diff("block", FromHex("3fe11f"), block);
        diagnostics.Assert("maximum table size", 4096, encoder.MaximumTableSize);
        CollectionAssert.AreEqual(FromHex("3fe11f"), block);
        Assert.AreEqual(4096, encoder.MaximumTableSize);
    }

    [TestMethod]
    public void Encode_PeerAllowsNoTable_IndexesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize(0);
        var fields = Fields(("x-a", "1"));
        diagnostics.Arrange("peer maximum table size", 0);
        diagnostics.Arrange("fields", string.Join(", ", fields));

        var block = encoder.Encode(fields);
        diagnostics.Bytes("encoded block", block);
        diagnostics.Act("table size", encoder.TableSize);

        diagnostics.Diff("block", FromHex("20 0003782d61 0131"), block);
        diagnostics.Assert("table size", 0, encoder.TableSize);
        CollectionAssert.AreEqual(FromHex("20 0003782d61 0131"), block);
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("fields", "null");

        var error = Assert.ThrowsExactly<ArgumentNullException>(() => new HpackEncoder().Encode(null!));
        diagnostics.Act("Encode(null) threw", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentNullException), error.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NegativeTableSize_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder(-1));
        diagnostics.Act("new HpackEncoder(-1) threw", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), error.GetType().Name);
    }

    [TestMethod]
    public void SetPeerMaximumTableSize_Negative_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("peer maximum table size", -1);

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder().SetPeerMaximumTableSize(-1));
        diagnostics.Act("SetPeerMaximumTableSize(-1) threw", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), error.GetType().Name);
    }
}
