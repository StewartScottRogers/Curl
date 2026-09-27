namespace Curl.Output;

/// <summary>
/// Pins that <see cref="DerReader" /> accepts and refuses the elements curl 8.21.0's
/// <c>getASN1Element</c> does.
/// </summary>
[TestClass]
public sealed class DerReaderTests
{
    [TestMethod]
    public void TryRead_ShortLength_FindsTheContent()
    {
        byte[] der = [0x00, 0x04, 0x02, 0xAA, 0xBB, 0xCC];

        Assert.IsTrue(DerReader.TryRead(der, 1, der.Length, out var element));

        Assert.AreEqual((4, false, 3, 5, 5), Describe(element));
        Assert.AreEqual(2, element.Length);
    }

    [TestMethod]
    public void TryRead_LongLength_FindsTheContent()
    {
        byte[] der = [0x30, 0x81, 0x02, 0xAA, 0xBB];

        Assert.IsTrue(DerReader.TryRead(der, 0, der.Length, out var element));

        Assert.AreEqual((16, true, 3, 5, 5), Describe(element));
    }

    [TestMethod]
    public void TryRead_IndefiniteLengthOnConstructedElement_RunsToTheZeroByteAndSkipsOnlyIt()
    {
        byte[] der = [0x30, 0x80, 0x02, 0x01, 0x05, 0x00, 0x00];

        Assert.IsTrue(DerReader.TryRead(der, 0, der.Length, out var element));

        Assert.AreEqual((16, true, 2, 5, 6), Describe(element));
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "nothing left")]
    [DataRow(new byte[] { 0x00, 0x00 }, DisplayName = "zero identifier")]
    [DataRow(new byte[] { 0x1F, 0x01, 0x00 }, DisplayName = "long-form tag")]
    [DataRow(new byte[] { 0x04 }, DisplayName = "no length")]
    [DataRow(new byte[] { 0x04, 0x80, 0x00 }, DisplayName = "indefinite primitive")]
    [DataRow(new byte[] { 0x30, 0x80, 0x1F, 0x00 }, DisplayName = "indefinite with unreadable child")]
    [DataRow(new byte[] { 0x30, 0x80, 0x02, 0x01, 0x05 }, DisplayName = "indefinite without end")]
    [DataRow(new byte[] { 0x04, 0x84, 0x01 }, DisplayName = "length bytes missing")]
    [DataRow(new byte[] { 0x04, 0x85, 0x01, 0x00, 0x00, 0x00, 0x00 }, DisplayName = "length past 32 bits")]
    [DataRow(new byte[] { 0x04, 0x05, 0x01 }, DisplayName = "content missing")]
    public void TryRead_ElementCurlRefuses_Fails(byte[] der)
    {
        Assert.IsFalse(DerReader.TryRead(der, 0, der.Length, out _));
    }

    [TestMethod]
    public void TryRead_SourceLongerThan256KiB_Fails()
    {
        var der = new byte[0x40001];
        der[0] = 0x04;
        der[1] = 0x01;

        Assert.IsFalse(DerReader.TryRead(der, 0, der.Length, out _));
    }

    [TestMethod]
    [DataRow(16, true)]
    [DataRow(17, false)]
    public void TryRead_IndefiniteNesting_StopsAtSixteenLevels(int levels, bool accepted)
    {
        byte[] der = [.. Enumerable.Repeat<byte[]>([0x30, 0x80], levels).SelectMany(pair => pair), .. new byte[levels + 1]];

        Assert.AreEqual(accepted, DerReader.TryRead(der, 0, der.Length, out _));
    }

    [TestMethod]
    public void Read_ElementCurlRefuses_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => DerReader.Read([0x00], 0, 1));
    }

    private static (int Tag, bool IsConstructed, int Start, int End, int Next) Describe(DerElement element) =>
        (element.Tag, element.IsConstructed, element.Start, element.End, element.Next);
}
