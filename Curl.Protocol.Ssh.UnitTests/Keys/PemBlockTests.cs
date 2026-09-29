namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins how <see cref="PemBlock" /> finds a key file's block, headers and body.
/// </summary>
[TestClass]
public sealed class PemBlockTests
{
    [TestMethod]
    public void Find_LegacyEncryptedBlock_ReadsItsHeadersAndBody()
    {
        PemBlock? block = PemBlock.Find("-----BEGIN RSA PRIVATE KEY-----\r\nProc-Type: 4,ENCRYPTED\r\nDEK-Info:AES-128-CBC,00\r\n\r\nAAEC\r\n-----END RSA PRIVATE KEY-----\r\n");

        Assert.IsNotNull(block);
        Assert.AreEqual("RSA PRIVATE KEY", block.Label);
        Assert.AreEqual("4,ENCRYPTED", block.Headers["Proc-Type"]);
        Assert.AreEqual("AES-128-CBC,00", block.Headers["DEK-Info"]);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, block.Body);
    }

    [TestMethod]
    public void Find_TwoBlocks_ReadsTheFirst()
    {
        PemBlock? block = PemBlock.Find("-----BEGIN A-----\nAA==\n-----END A-----\n-----BEGIN B-----\nAQ==\n-----END B-----\n");

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
        Assert.IsNull(PemBlock.Find(text));
    }

    [TestMethod]
    public void Find_HeadersAndNoBody_ReadsAnEmptyBody()
    {
        PemBlock? block = PemBlock.Find("-----BEGIN A-----\nName: value\n-----END A-----\n");

        Assert.AreEqual("value", block!.Headers["Name"]);
        Assert.IsEmpty(block.Body);
    }

    [TestMethod]
    public void Find_ColonFirstOnALine_IsBodyNotAHeader()
    {
        Assert.ThrowsExactly<FormatException>(() => PemBlock.Find("-----BEGIN A-----\n:AA==\n-----END A-----\n"));
    }
}
