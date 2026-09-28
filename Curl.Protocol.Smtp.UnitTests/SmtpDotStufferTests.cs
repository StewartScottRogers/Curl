namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins that <see cref="SmtpDotStuffer" /> carries its state from one chunk of the upload to
/// the next, so a line start split across two reads is still stuffed (BL-542).
/// </summary>
[TestClass]
public sealed class SmtpDotStufferTests
{
    [TestMethod]
    public void Encode_CrlfAndDotInSeparateChunks_StuffsTheDot()
    {
        var stuffer = new SmtpDotStuffer();

        byte[] first = stuffer.Encode("a\r"u8);
        byte[] second = stuffer.Encode("\n"u8);
        byte[] third = stuffer.Encode(".b"u8);

        CollectionAssert.AreEqual("a\r"u8.ToArray(), first);
        CollectionAssert.AreEqual("\n"u8.ToArray(), second);
        CollectionAssert.AreEqual("..b"u8.ToArray(), third);
        CollectionAssert.AreEqual("\r\n.\r\n"u8.ToArray(), stuffer.EndOfData);
    }

    [TestMethod]
    public void EndOfData_ChunkEndingInCrlf_IsTheShortMark()
    {
        var stuffer = new SmtpDotStuffer();

        stuffer.Encode("a\r\n"u8);

        CollectionAssert.AreEqual(".\r\n"u8.ToArray(), stuffer.EndOfData);
    }
}
