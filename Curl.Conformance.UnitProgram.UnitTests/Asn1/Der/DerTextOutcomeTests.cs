using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Asn1.Der;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Der;

[TestClass]
public sealed class DerTextOutcomeTests
{
    [TestMethod]
    public void Print_TextShorterThanTheBuffer_IsOkWithItsUtf8Bytes()
    {
        DerTextOutcome outcome = DerTextOutcome.Print(() => "åb", 4);

        Assert.AreEqual(CurlCode.Ok, outcome.Code);
        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA5, 0x62 }, outcome.Text);
    }

    [TestMethod]
    public void Print_TextThatWithItsNulFillsMoreThanTheBuffer_IsTooLargeWithNoText()
    {
        DerTextOutcome outcome = DerTextOutcome.Print(() => "åb", 3);

        Assert.AreEqual(CurlCode.TooLarge, outcome.Code);
        Assert.IsEmpty(outcome.Text);
    }

    [TestMethod]
    public void Print_NullPrinter_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DerTextOutcome.Print(null!, 1));
    }
}
