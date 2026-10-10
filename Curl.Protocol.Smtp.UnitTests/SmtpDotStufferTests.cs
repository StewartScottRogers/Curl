using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins that <see cref="SmtpDotStuffer" /> carries its state from one chunk of the upload to
/// the next, so a line start split across two reads is still stuffed (BL-542).
/// </summary>
[TestClass]
public sealed class SmtpDotStufferTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Encode_CrlfAndDotInSeparateChunks_StuffsTheDot()
    {
        Diagnostics.Arrange("chunks", "\"a\\r\", \"\\n\", \".b\"");
        var stuffer = new SmtpDotStuffer();

        byte[] first = stuffer.Encode("a\r"u8);
        byte[] second = stuffer.Encode("\n"u8);
        byte[] third = stuffer.Encode(".b"u8);
        Diagnostics.Bytes("first encoded", first);
        Diagnostics.Bytes("second encoded", second);
        Diagnostics.Bytes("third encoded", third);
        Diagnostics.Act("end of data", stuffer.EndOfData.Length + " bytes");

        Diagnostics.Diff("first", "a\r"u8, first);
        CollectionAssert.AreEqual("a\r"u8.ToArray(), first);
        Diagnostics.Diff("second", "\n"u8, second);
        CollectionAssert.AreEqual("\n"u8.ToArray(), second);
        Diagnostics.Diff("third", "..b"u8, third);
        CollectionAssert.AreEqual("..b"u8.ToArray(), third);
        Diagnostics.Diff("end of data", "\r\n.\r\n"u8, stuffer.EndOfData);
        CollectionAssert.AreEqual("\r\n.\r\n"u8.ToArray(), stuffer.EndOfData);
    }

    [TestMethod]
    public void Encode_ConvertingLineFeeds_InsertsCrBeforeEachBareLfAcrossChunks()
    {
        Diagnostics.Arrange("chunks", "\"From: x\\n.a\\r\", \"\\nb\\n\"");
        var stuffer = new SmtpDotStuffer(convertsLineFeeds: true);

        byte[] first = stuffer.Encode("From: x\n.a\r"u8);
        byte[] second = stuffer.Encode("\nb\n"u8);
        Diagnostics.Bytes("first encoded", first);
        Diagnostics.Bytes("second encoded", second);
        Diagnostics.Act("end of data", stuffer.EndOfData.Length + " bytes");

        Diagnostics.Diff("first", "From: x\r\n..a\r"u8, first);
        CollectionAssert.AreEqual("From: x\r\n..a\r"u8.ToArray(), first);
        Diagnostics.Diff("second", "\nb\r\n"u8, second);
        CollectionAssert.AreEqual("\nb\r\n"u8.ToArray(), second);
        Diagnostics.Diff("end of data", ".\r\n"u8, stuffer.EndOfData);
        CollectionAssert.AreEqual(".\r\n"u8.ToArray(), stuffer.EndOfData);
    }

    [TestMethod]
    public void EndOfData_ChunkEndingInCrlf_IsTheShortMark()
    {
        Diagnostics.Arrange("chunk", "\"a\\r\\n\"");
        var stuffer = new SmtpDotStuffer();

        stuffer.Encode("a\r\n"u8);
        Diagnostics.Bytes("end of data", stuffer.EndOfData);
        Diagnostics.Act("end of data", stuffer.EndOfData.Length + " bytes");

        Diagnostics.Diff("end of data", ".\r\n"u8, stuffer.EndOfData);
        CollectionAssert.AreEqual(".\r\n"u8.ToArray(), stuffer.EndOfData);
    }
}
