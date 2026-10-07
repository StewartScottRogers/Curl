using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins that <see cref="DerReader" /> accepts and refuses the elements curl 8.21.0's
/// <c>getASN1Element</c> does.
/// </summary>
[TestClass]
public sealed class DerReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryRead_ShortLength_FindsTheContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = [0x00, 0x04, 0x02, 0xAA, 0xBB, 0xCC];
        diagnostics.Bytes("der", der);
        diagnostics.Arrange("offset", 1);

        var read = DerReader.TryRead(der, 1, der.Length, out var element);

        diagnostics.Act("read", read);
        diagnostics.Act("element", Describe(element));
        diagnostics.Assert("read", true, read);
        diagnostics.Assert("element", (4, false, 3, 5, 5), Describe(element));
        Assert.IsTrue(read);

        Assert.AreEqual((4, false, 3, 5, 5), Describe(element));
        Assert.AreEqual(2, element.Length);
    }

    [TestMethod]
    public void TryRead_LongLength_FindsTheContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = [0x30, 0x81, 0x02, 0xAA, 0xBB];
        diagnostics.Bytes("der", der);
        diagnostics.Arrange("offset", 0);

        var read = DerReader.TryRead(der, 0, der.Length, out var element);

        diagnostics.Act("read", read);
        diagnostics.Act("element", Describe(element));
        diagnostics.Assert("read", true, read);
        diagnostics.Assert("element", (16, true, 3, 5, 5), Describe(element));
        Assert.IsTrue(read);

        Assert.AreEqual((16, true, 3, 5, 5), Describe(element));
    }

    [TestMethod]
    public void TryRead_IndefiniteLengthOnConstructedElement_RunsToTheZeroByteAndSkipsOnlyIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = [0x30, 0x80, 0x02, 0x01, 0x05, 0x00, 0x00];
        diagnostics.Bytes("der", der);
        diagnostics.Arrange("offset", 0);

        var read = DerReader.TryRead(der, 0, der.Length, out var element);

        diagnostics.Act("read", read);
        diagnostics.Act("element", Describe(element));
        diagnostics.Assert("read", true, read);
        diagnostics.Assert("element", (16, true, 2, 5, 6), Describe(element));
        Assert.IsTrue(read);

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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("der", der);
        diagnostics.Arrange("offset", 0);

        var read = DerReader.TryRead(der, 0, der.Length, out _);

        diagnostics.Act("read", read);
        diagnostics.Assert("read", false, read);
        Assert.IsFalse(read);
    }

    [TestMethod]
    public void TryRead_SourceLongerThan256KiB_Fails()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var der = new byte[0x40001];
        der[0] = 0x04;
        der[1] = 0x01;
        diagnostics.Arrange("source length", der.Length);
        diagnostics.Bytes("der", der);

        var read = DerReader.TryRead(der, 0, der.Length, out _);

        diagnostics.Act("read", read);
        diagnostics.Assert("read", false, read);
        Assert.IsFalse(read);
    }

    [TestMethod]
    [DataRow(16, true)]
    [DataRow(17, false)]
    public void TryRead_IndefiniteNesting_StopsAtSixteenLevels(int levels, bool accepted)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("levels", levels);
        diagnostics.Arrange("accepted", accepted);
        byte[] der = [.. Enumerable.Repeat<byte[]>([0x30, 0x80], levels).SelectMany(pair => pair), .. new byte[levels + 1]];
        diagnostics.Bytes("der", der);

        var read = DerReader.TryRead(der, 0, der.Length, out _);

        diagnostics.Act("read", read);
        diagnostics.Assert("read", accepted, read);
        Assert.AreEqual(accepted, read);
    }

    [TestMethod]
    public void Read_ElementCurlRefuses_ThrowsFormatException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("der", "00");

        var exception = Assert.ThrowsExactly<FormatException>(() => DerReader.Read([0x00], 0, 1));

        diagnostics.Act("exception type", exception.GetType().Name);
        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("exception type", nameof(FormatException), exception.GetType().Name);
    }

    private static (int Tag, bool IsConstructed, int Start, int End, int Next) Describe(DerElement element) =>
        (element.Tag, element.IsConstructed, element.Start, element.End, element.Next);
}
