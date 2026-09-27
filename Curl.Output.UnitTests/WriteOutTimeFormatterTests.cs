namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTimeFormatter"/> to what curl 8.21.0 (mingw, Schannel) wrote for
/// <c>-w "[%time{…}]"</c> on 2026-09-26; the commands are in BL-279's Notes.
/// </summary>
[TestClass]
public sealed class WriteOutTimeFormatterTests
{
    // Sunday 2026-09-27 03:30:08.545957 UTC, the instant of the reference measurements.
    private static readonly FixedTimeProvider Measured = new(new DateTimeOffset(2026, 9, 27, 3, 30, 8, TimeSpan.Zero).AddTicks(5_459_570));

    // Sunday 2005-01-02 15:04:05.000007 UTC, where every number has a leading zero to drop.
    private static readonly FixedTimeProvider LeadingZeros = new(new DateTimeOffset(2005, 1, 2, 15, 4, 5, TimeSpan.Zero).AddTicks(70));

    [TestMethod]
    [DataRow("%a|%A|%b|%B", "Sun|Sunday|Sep|September")]
    [DataRow("%c", "9/27/2026 3:30:08 AM")]
    [DataRow("%x|%X", "9/27/2026|3:30:08 AM")]
    [DataRow("%d|%H|%I|%j|%m|%M|%S|%p", "27|03|03|270|09|30|08|AM")]
    [DataRow("%U|%w|%W|%y|%Y", "39|0|38|26|2026")]
    [DataRow("%z|%Z|%%|%f|%s", "+0000|UTC|%|545957|1790479808")]
    [DataRow("%%f", "%f")]
    [DataRow("abc", "abc")]
    [DataRow("", "")]
    public void Format_MeasuredConversions_MatchCurl(string format, string expected)
    {
        Assert.AreEqual(expected, WriteOutTimeFormatter.Format(format, Measured));
    }

    [TestMethod]
    [DataRow("%#c", "Sunday, September 27, 2026 3:30:08 AM")]
    [DataRow("%#x|%#X", "Sunday, September 27, 2026|3:30:08 AM")]
    [DataRow("%#a|%#A|%#b|%#B|%#p|%#%", "Sun|Sunday|Sep|September|AM|%")]
    [DataRow("%#d|%#H|%#I|%#j|%#m|%#M|%#S|%#U|%#w|%#W|%#y|%#Y", "27|3|3|270|9|30|8|39|0|38|26|2026")]
    [DataRow("%#z|%#Z", FixedTimeProvider.TimeZoneStandardName + "|" + FixedTimeProvider.TimeZoneStandardName)]
    public void Format_AlternateConversions_MatchCurl(string format, string expected)
    {
        Assert.AreEqual(expected, WriteOutTimeFormatter.Format(format, Measured));
    }

    [TestMethod]
    [DataRow("%d|%H|%I|%j|%m|%M|%S|%U|%W|%y|%p|%f", "02|15|03|002|01|04|05|01|00|05|PM|000007")]
    [DataRow("%#d|%#H|%#I|%#j|%#m|%#M|%#S|%#U|%#W|%#y", "2|15|3|2|1|4|5|1|0|5")]
    public void Format_LeadingZeros_AreDroppedOnlyByTheAlternateFlag(string format, string expected)
    {
        Assert.AreEqual(expected, WriteOutTimeFormatter.Format(format, LeadingZeros));
    }

    [TestMethod]
    [DataRow("abc%Qdef")]
    [DataRow("%C")]
    [DataRow("%e")]
    [DataRow("%n%t")]
    [DataRow("%Ey")]
    [DataRow("%#f")]
    [DataRow("%#s")]
    [DataRow("%#Q")]
    [DataRow("%Y%")]
    [DataRow("x%#")]
    public void Format_ConversionTheRuntimeRejects_RendersNothing(string format)
    {
        Assert.AreEqual(string.Empty, WriteOutTimeFormatter.Format(format, Measured));
    }

    [TestMethod]
    public void Format_ResultOf255Bytes_Fits()
    {
        string format = new('a', 255);

        Assert.AreEqual(format, WriteOutTimeFormatter.Format(format, Measured));
    }

    [TestMethod]
    public void Format_ResultOf256Bytes_RendersNothing()
    {
        Assert.AreEqual(string.Empty, WriteOutTimeFormatter.Format(new string('a', 256), Measured));
    }

    [TestMethod]
    public void Format_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format(null!, Measured));
        Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format("%Y", null!));
    }
}
