using System.Globalization;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins <see cref="FtpModificationTime.TryReadDate" /> to every row of curl 8.22.0's
/// <c>tests/unit/unit1668.c</c> (ADR-0482), and <see cref="FtpModificationTime.Of" /> to the
/// times real curl's <c>-I</c> printed for odd <c>MDTM</c> replies (BL-2904, ADR-0725).
/// </summary>
[TestClass]
public sealed class FtpModificationTimeTests
{
    [TestMethod]
    [DataRow("19980320114234.123", "1998-03-20 11:42:34")]
    [DataRow("19980320114234.000", "1998-03-20 11:42:34")]
    [DataRow("19980320114234.aaa", "1998-03-20 11:42:34")]
    [DataRow("19980320114234.999", "1998-03-20 11:42:34")]
    [DataRow("19980320114234", "1998-03-20 11:42:34")]
    [DataRow("09980320114234", "0998-03-20 11:42:34")]
    [DataRow("10980320114234", "1098-03-20 11:42:34")]
    [DataRow("19080320114234", "1908-03-20 11:42:34")]
    [DataRow("19900320114234", "1990-03-20 11:42:34")]
    [DataRow("19980020114234", "1998-00-20 11:42:34")]
    [DataRow("19980300114234", "1998-03-00 11:42:34")]
    [DataRow("19980320014234", "1998-03-20 01:42:34")]
    [DataRow("19980320110234", "1998-03-20 11:02:34")]
    [DataRow("19980320114204", "1998-03-20 11:42:04")]
    [DataRow("19980320114230", "1998-03-20 11:42:30")]
    [DataRow("19980231114234", "1998-02-31 11:42:34")]
    [DataRow("19980320114234--", "1998-03-20 11:42:34")]
    [DataRow("20260320114234.123", "2026-03-20 11:42:34")]
    [DataRow("30260320114234.123", "3026-03-20 11:42:34")]
    [DataRow("40260320114234.123", "4026-03-20 11:42:34")]
    [DataRow("19980320114234ÿ", "1998-03-20 11:42:34")]
    public void TryReadDate_Unit1668AcceptedRow_GivesTheSixFields(string text, string expected)
    {
        bool read = FtpModificationTime.TryReadDate(text, out FtpMdtmDate date);

        Assert.IsTrue(read);
        Assert.AreEqual(expected, Format(date));
    }

    [TestMethod]
    [DataRow("19980320256565")]
    [DataRow("19980320236565")]
    [DataRow("19980320235865")]
    [DataRow(":9980231114234")]
    [DataRow("a9980231114234")]
    [DataRow("1:980231114234")]
    [DataRow("1a980231114234")]
    [DataRow("19:80231114234")]
    [DataRow("19b80231114234")]
    [DataRow("199:0231114234")]
    [DataRow("199c0231114234")]
    [DataRow("1998:231114234")]
    [DataRow("1998d231114234")]
    [DataRow("19980:31114234")]
    [DataRow("19980e31114234")]
    [DataRow("199802:1114234")]
    [DataRow("199802f1114234")]
    [DataRow("1998023:114234")]
    [DataRow("1998023/114234")]
    [DataRow("19980231:14234")]
    [DataRow("19980231x14234")]
    [DataRow("199802311:4234")]
    [DataRow("199802311z4234")]
    [DataRow("1998023111:234")]
    [DataRow("1998023111&234")]
    [DataRow("19980231114:34")]
    [DataRow("19980231114#34")]
    [DataRow("199802311142:4")]
    [DataRow("199802311142!4")]
    [DataRow("1998023111423:")]
    [DataRow("1998023111423@")]
    [DataRow(" 19980320114234")]
    [DataRow("1998032011423")]
    [DataRow("199803201142")]
    [DataRow("19980320114")]
    [DataRow("199803201")]
    [DataRow("19980320")]
    [DataRow("1998032")]
    [DataRow("199803")]
    [DataRow("19980")]
    [DataRow("1998")]
    [DataRow("199")]
    [DataRow("19")]
    [DataRow("1")]
    [DataRow("")]
    [DataRow("1998~320114234ÿ")]
    [DataRow("1998\u007f320114234ÿ")]
    [DataRow("1998\u0080320114234ÿ")]
    [DataRow("1998\u0081320114234ÿ")]
    [DataRow("1998\u0082320114234ÿ")]
    [DataRow("Ì")]
    [DataRow("U")]
    public void TryReadDate_Unit1668RefusedRow_IsFalse(string text)
    {
        bool read = FtpModificationTime.TryReadDate(text, out FtpMdtmDate date);

        Assert.IsFalse(read);
        Assert.AreEqual(default, date);
    }

    [TestMethod]
    [DataRow("213 20260927123456", "2026-09-27T12:34:56")]
    [DataRow("213 20260927123456.789 trailing", "2026-09-27T12:34:56")]
    [DataRow("213 19980231114234", "1998-03-03T11:42:34")]
    [DataRow("213 19980300114234", "1998-02-28T11:42:34")]
    [DataRow("213 19980320115960", "1998-03-20T12:00:00")]
    [DataRow("213 15830101000000", "1583-01-01T00:00:00")]
    [DataRow("213 99991231235959", "9999-12-31T23:59:59")]
    public void Of_TimestampCurlCanDate_IsTheTimeCurlPrints(string line, string expected)
    {
        DateTimeOffset? modified = FtpModificationTime.Of(new FtpReply(213, line));

        Assert.AreEqual(
            DateTimeOffset.Parse(expected + "Z", CultureInfo.InvariantCulture),
            modified);
    }

    [TestMethod]
    [DataRow(213, "213 19980020114234")]
    [DataRow(213, "213 09980320114234")]
    [DataRow(213, "213 15821231235959")]
    [DataRow(213, "213 99991231235960")]
    [DataRow(213, "213 99991232000000")]
    [DataRow(213, "213 2026092712345")]
    [DataRow(213, "213")]
    [DataRow(550, "550 20260927123456")]
    public void Of_TimestampCurlCannotDate_IsUnknown(int code, string line)
    {
        Assert.IsNull(FtpModificationTime.Of(new FtpReply(code, line)));
    }

    private static string Format(FtpMdtmDate date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{date.Year:0000}-{date.Month:00}-{date.Day:00} {date.Hour:00}:{date.Minute:00}:{date.Second:00}");
}
