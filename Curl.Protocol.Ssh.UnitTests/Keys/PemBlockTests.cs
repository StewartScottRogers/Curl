using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins how <see cref="PemBlock" /> finds a key file's block, headers and body.
/// </summary>
[TestClass]
public sealed class PemBlockTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Find_LegacyEncryptedBlock_ReadsItsHeadersAndBody()
    {
        const string Text = "-----BEGIN RSA PRIVATE KEY-----\r\nProc-Type: 4,ENCRYPTED\r\nDEK-Info:AES-128-CBC,00\r\n\r\nAAEC\r\n-----END RSA PRIVATE KEY-----\r\n";
        Diagnostics.ArrangeText("text", Text);

        PemBlock? block = PemBlock.Find(Text);

        ActBlock(block);
        Diagnostics.Assert("label", "RSA PRIVATE KEY", block?.Label);
        Diagnostics.AssertBytes("body", [0, 1, 2], block?.Body);
        Assert.IsNotNull(block);
        Assert.AreEqual("RSA PRIVATE KEY", block.Label);
        Assert.AreEqual("4,ENCRYPTED", block.Headers["Proc-Type"]);
        Assert.AreEqual("AES-128-CBC,00", block.Headers["DEK-Info"]);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, block.Body);
    }

    [TestMethod]
    public void Find_TwoBlocks_ReadsTheFirst()
    {
        const string Text = "-----BEGIN A-----\nAA==\n-----END A-----\n-----BEGIN B-----\nAQ==\n-----END B-----\n";
        Diagnostics.ArrangeText("text", Text);

        PemBlock? block = PemBlock.Find(Text);

        ActBlock(block);
        Diagnostics.Assert("label", "A", block?.Label);
        Diagnostics.AssertBytes("body", [0], block?.Body);
        Assert.AreEqual("A", block!.Label);
        Assert.IsEmpty(block.Headers);
        CollectionAssert.AreEqual(new byte[] { 0 }, block.Body);
    }

    [TestMethod]
    [DataRow("no block at all", DisplayName = "no BEGIN line")]
    [DataRow("-----BEGIN -----\nAA==\n-----END -----\n", DisplayName = "empty label")]
    [DataRow("-----BEGIN A----\nAA==\n-----END A-----\n", DisplayName = "BEGIN line not closed with five dashes")]
    [DataRow("-----BEGIN A-----\nAA==\n-----END B-----\n", DisplayName = "END names another label")]
    public void Find_NoCompleteBlock_ReturnsNull(string text)
    {
        Diagnostics.ArrangeText("text", text);

        PemBlock? block = PemBlock.Find(text);

        ActBlock(block);
        Diagnostics.Assert("block", "(none)", block?.Label ?? "(none)");
        Assert.IsNull(block);
    }

    [TestMethod]
    public void Find_HeadersAndNoBody_ReadsAnEmptyBody()
    {
        const string Text = "-----BEGIN A-----\nName: value\n-----END A-----\n";
        Diagnostics.ArrangeText("text", Text);

        PemBlock? block = PemBlock.Find(Text);

        ActBlock(block);
        Diagnostics.Assert("body length", 0, block?.Body.Length);
        Assert.AreEqual("value", block!.Headers["Name"]);
        Assert.IsEmpty(block.Body);
    }

    [TestMethod]
    public void Find_ColonFirstOnALine_IsBodyNotAHeader()
    {
        const string Text = "-----BEGIN A-----\n:AA==\n-----END A-----\n";
        Diagnostics.ArrangeText("text", Text);

        var failure = Assert.ThrowsExactly<FormatException>(() => PemBlock.Find(Text));

        Diagnostics.ActAndAssertThrown(nameof(FormatException), failure);
    }

    private void ActBlock(PemBlock? block)
    {
        Diagnostics.Act("block", block is null
            ? "(none)"
            : $"{block.Label}, headers [{string.Join(", ", block.Headers.Select(header => $"{header.Key}: {header.Value}"))}]");
        if (block is not null)
        {
            Diagnostics.Bytes("body", block.Body);
        }
    }
}
