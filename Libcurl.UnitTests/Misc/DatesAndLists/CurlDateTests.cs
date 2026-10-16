using Libcurl.Misc.DatesAndLists;

namespace Libcurl.UnitTests.Misc.DatesAndLists;

[TestClass]
public sealed class CurlDateTests
{
    // Upstream's accepted formats, as curl_getdate's man page and test517 list them.
    [TestMethod]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", 784111777L)]
    [DataRow("Sunday, 06-Nov-94 08:49:37 GMT", 784111777L)]
    [DataRow("Sun Nov  6 08:49:37 1994", 784111777L)]
    [DataRow("06 Nov 1994 08:49:37 GMT", 784111777L)]
    [DataRow("1994 Nov 6 08:49:37", 784111777L)]
    [DataRow("GMT 08:49:37 06-Nov-94 Sunday", 784111777L)]
    [DataRow("1994 Nov 6", 784080000L)]
    [DataRow("06-Nov-94", 784080000L)]
    [DataRow("Sun/Nov/6/94/GMT", 784080000L)]
    [DataRow("Sun, 06 Nov 1994 08:49:37 CET", 784108177L)]
    [DataRow("06 Nov 1994 08:49:37 EST", 784129777L)]
    [DataRow("Sun, 12 Sep 2004 15:05:58 -0700", 1095026758L)]
    [DataRow("Sat, 11 Sep 2004 21:32:11 +0200", 1094931131L)]
    [DataRow("20040912 15:05:58 -0700", 1095026758L)]
    [DataRow("20040911 +0200", 1094853600L)]
    [DataRow("31 Feb 2030", 1898726400L)]
    [DataRow("Thu, 01-Jan-1970 00:00:00 GMT", 0L)]
    [DataRow("Thu, 01-Jan-1970 00:59:59 GMT", 3599L)]
    [DataRow("Thu, 31-Dec-1969 23:59:58 GMT", -2L)]
    public void Parse_AcceptedFormat_AnswersSecondsSinceTheEpoch(string text, long expected)
    {
        Assert.AreEqual(expected, CurlDate.Parse(text));
    }

    [TestMethod]
    public void Parse_OneSecondBeforeTheEpoch_AnswersZeroNotTheFailureValue()
    {
        Assert.AreEqual(0L, CurlDate.Parse("Thu, 31-Dec-1969 23:59:59 GMT"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not a date")]
    [DataRow("1994")]
    [DataRow("Sun Nov 6")]
    [DataRow("Sun, 32 Nov 1994 08:49:37 GMT")]
    [DataRow("Sun, 06 Nov 1994 24:49:37 GMT")]
    [DataRow("06 Nov 1582")]
    [DataRow("06 Nov 123456789")]
    public void Parse_TextThatIsNotADate_AnswersMinusOne(string text)
    {
        Assert.AreEqual(-1L, CurlDate.Parse(text));
        Assert.AreEqual(CurlDate.Invalid, CurlDate.Parse(text));
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlDate.Parse(null!));
    }
}
