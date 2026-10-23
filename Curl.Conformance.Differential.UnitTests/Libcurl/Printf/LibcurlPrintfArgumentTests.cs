using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Printf;

[TestClass]
public sealed class LibcurlPrintfArgumentTests
{
    [TestMethod]
    [DataRow('i', -1L, -1)]
    [DataRow('l', 7L, 7)]
    [DataRow('c', 65L, 65)]
    public void ToCurlArgument_IntKinds_IsAnInt(char kind, long number, int expected) =>
        Assert.AreEqual(expected, new LibcurlPrintfArgument(kind, number, null).ToCurlArgument());

    [TestMethod]
    public void ToCurlArgument_NullInt_IsNull() =>
        Assert.IsNull(new LibcurlPrintfArgument('i', null, null).ToCurlArgument());

    [TestMethod]
    public void ToCurlArgument_LongLong_IsALong() =>
        Assert.AreEqual(9000000000L, new LibcurlPrintfArgument('q', 9000000000, null).ToCurlArgument());

    [TestMethod]
    public void ToCurlArgument_Pointer_IsALongOrNull()
    {
        Assert.AreEqual(16L, new LibcurlPrintfArgument('p', 16, null).ToCurlArgument());
        Assert.IsNull(new LibcurlPrintfArgument('p', null, null).ToCurlArgument());
    }

    [TestMethod]
    public void ToCurlArgument_Unsigned_IsTheBitsAsAULong() =>
        Assert.AreEqual(ulong.MaxValue, new LibcurlPrintfArgument('u', -1, null).ToCurlArgument());

    [TestMethod]
    public void ToCurlArgument_Double_IsTheDoubleOfItsBits() =>
        Assert.AreEqual(1.5, new LibcurlPrintfArgument('d', 0x3ff8000000000000, null).ToCurlArgument());

    [TestMethod]
    public void ToCurlArgument_String_IsItsText() =>
        Assert.AreEqual("hi", new LibcurlPrintfArgument('s', null, "hi").ToCurlArgument());

    [TestMethod]
    public void Slot_NumberOrNull_IsTheNumberOrZero()
    {
        Assert.AreEqual(0x3ff8000000000000, new LibcurlPrintfArgument('d', 0x3ff8000000000000, null).Slot);
        Assert.AreEqual(0, new LibcurlPrintfArgument('p', null, null).Slot);
    }
}
