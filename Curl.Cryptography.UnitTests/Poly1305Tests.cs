using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Poly1305" /> to RFC 8439's published vectors (section 2.5.2 and
/// Appendix A.3 test vectors #1 to #11) and checks <see cref="Poly1305.Verify" /> rejects
/// a flipped bit (ADR-0118).
/// </summary>
[TestClass]
public sealed class Poly1305Tests
{
    private const string ZeroBlock = "00000000000000000000000000000000";

    private const string ForumKey = "85d6be7857556d337f4452fe42d506a80103808afb0db2fd4abff6af4149f51b";

    private const string ForumMessage = "43727970746f6772617068696320466f72756d2052657365617263682047726f7570";

    private const string ForumTag = "a8061dc1305136c6c22b8baf0c0127a9";

    private const string IetfContribution =
        "416e79207375626d697373696f6e20746f20746865204945544620696e74656e6465642062792074686520436f6e7472696275746f7220666f72207075626c69636174696f6e20617320616c6c206f722070617274206f6620616e204945544620496e7465726e65742d4472616674206f722052464320616e6420616e792073746174656d656e74206d6164652077697468696e2074686520636f6e74657874206f6620616e204945544620616374697669747920697320636f6e7369646572656420616e20224945544620436f6e747269627574696f6e222e20537563682073746174656d656e747320696e636c756465206f72616c2073746174656d656e747320696e20494554462073657373696f6e732c2061732077656c6c206173207772697474656e20616e6420656c656374726f6e696320636f6d6d756e69636174696f6e73206d61646520617420616e792074696d65206f7220706c6163652c207768696368206172652061646472657373656420746f";

    private const string Jabberwocky =
        "2754776173206272696c6c69672c20616e642074686520736c6974687920746f7665730a446964206779726520616e642067696d626c6520696e2074686520776162653a0a416c6c206d696d737920776572652074686520626f726f676f7665732c0a416e6420746865206d6f6d65207261746873206f757467726162652e";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 8439 section 2.5.2, then Appendix A.3 test vectors #1 to #11 (key is r then s).
    [TestMethod]
    [DataRow(ForumKey, ForumMessage, ForumTag)]
    [DataRow(ZeroBlock + ZeroBlock, ZeroBlock + ZeroBlock + ZeroBlock + ZeroBlock, ZeroBlock)]
    [DataRow(ZeroBlock + "36e5f6b5c5e06070f0efca96227a863e", IetfContribution, "36e5f6b5c5e06070f0efca96227a863e")]
    [DataRow("36e5f6b5c5e06070f0efca96227a863e" + ZeroBlock, IetfContribution, "f3477e7cd95417af89a6b8794c310cf0")]
    [DataRow("1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0", Jabberwocky, "4541669a7eaaee61e708dc7cbcc5eb62")]
    [DataRow("02000000000000000000000000000000" + ZeroBlock, "ffffffffffffffffffffffffffffffff", "03000000000000000000000000000000")]
    [DataRow("02000000000000000000000000000000" + "ffffffffffffffffffffffffffffffff", "02000000000000000000000000000000", "03000000000000000000000000000000")]
    [DataRow("01000000000000000000000000000000" + ZeroBlock,
        "ffffffffffffffffffffffffffffffff" + "f0ffffffffffffffffffffffffffffff" + "11000000000000000000000000000000",
        "05000000000000000000000000000000")]
    [DataRow("01000000000000000000000000000000" + ZeroBlock,
        "ffffffffffffffffffffffffffffffff" + "fbfefefefefefefefefefefefefefefe" + "01010101010101010101010101010101",
        ZeroBlock)]
    [DataRow("02000000000000000000000000000000" + ZeroBlock, "fdffffffffffffffffffffffffffffff", "faffffffffffffffffffffffffffffff")]
    [DataRow("01000000000000000400000000000000" + ZeroBlock,
        "e33594d7505e43b90000000000000000" + "3394d7505e4379cd0100000000000000" + ZeroBlock + "01000000000000000000000000000000",
        "14000000000000005500000000000000")]
    [DataRow("01000000000000000400000000000000" + ZeroBlock,
        "e33594d7505e43b90000000000000000" + "3394d7505e4379cd0100000000000000" + ZeroBlock,
        "13000000000000000000000000000000")]
    public void ComputeTag_Rfc8439Vector_GivesThePublishedTag(string key, string message, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] tag = new byte[Poly1305.TagSize];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.5.2 and Appendix A.3");
        diagnostics.Bytes("key (r then s)", Convert.FromHexString(key));
        diagnostics.Bytes("message", Convert.FromHexString(message));

        Poly1305.ComputeTag(Convert.FromHexString(key), Convert.FromHexString(message), tag);
        diagnostics.Act("tag", Convert.ToHexStringLower(tag));

        diagnostics.Diff("tag", Convert.FromHexString(expected), tag);
        Assert.AreEqual(expected, Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    public void ComputeTag_EmptyMessage_GivesS()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] tag = new byte[Poly1305.TagSize];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.5.2 key, empty message");
        diagnostics.Bytes("key (r then s)", Convert.FromHexString(ForumKey));

        Poly1305.ComputeTag(Convert.FromHexString(ForumKey), [], tag);
        diagnostics.Act("tag", Convert.ToHexStringLower(tag));

        diagnostics.Diff("tag", Convert.FromHexString("0103808afb0db2fd4abff6af4149f51b"), tag);
        Assert.AreEqual("0103808afb0db2fd4abff6af4149f51b", Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    public void Verify_Rfc8439Section252Tag_ReturnsTrue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "RFC 8439 section 2.5.2");
        diagnostics.Bytes("key (r then s)", Convert.FromHexString(ForumKey));
        diagnostics.Bytes("message", Convert.FromHexString(ForumMessage));
        diagnostics.Bytes("tag", Convert.FromHexString(ForumTag));

        bool verified = Poly1305.Verify(Convert.FromHexString(ForumKey), Convert.FromHexString(ForumMessage), Convert.FromHexString(ForumTag));
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", true, verified);
        Assert.IsTrue(Poly1305.Verify(Convert.FromHexString(ForumKey), Convert.FromHexString(ForumMessage), Convert.FromHexString(ForumTag)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(15)]
    public void Verify_TagWithAFlippedBit_ReturnsFalse(int flippedByte)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] tag = Convert.FromHexString(ForumTag);
        tag[flippedByte] ^= 0x80;
        diagnostics.Arrange("vector source", "RFC 8439 section 2.5.2, tag with bit 7 of one byte flipped");
        diagnostics.Arrange("flipped byte", flippedByte);
        diagnostics.Bytes("tag", tag);

        bool verified = Poly1305.Verify(Convert.FromHexString(ForumKey), Convert.FromHexString(ForumMessage), tag);
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", false, verified);
        Assert.IsFalse(Poly1305.Verify(Convert.FromHexString(ForumKey), Convert.FromHexString(ForumMessage), tag));
    }

    [TestMethod]
    public void Verify_MessageWithAFlippedBit_ReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = Convert.FromHexString(ForumMessage);
        message[^1] ^= 1;
        diagnostics.Arrange("vector source", "RFC 8439 section 2.5.2, message with its last bit flipped");
        diagnostics.Bytes("message", message);

        bool verified = Poly1305.Verify(Convert.FromHexString(ForumKey), message, Convert.FromHexString(ForumTag));
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", false, verified);
        Assert.IsFalse(Poly1305.Verify(Convert.FromHexString(ForumKey), message, Convert.FromHexString(ForumTag)));
    }

    [TestMethod]
    [DataRow(31, 16)]
    [DataRow(32, 15)]
    public void ComputeTag_WrongLength_Throws(int keyLength, int tagLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", keyLength);
        diagnostics.Arrange("tag length", tagLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Poly1305.ComputeTag(new byte[keyLength], [], new byte[tagLength]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void Verify_TagIsNot16Bytes_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tag length", Poly1305.TagSize + 1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Poly1305.Verify(new byte[Poly1305.KeySize], [], new byte[Poly1305.TagSize + 1]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }
}
