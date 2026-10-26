using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-017 (BL-3546): curl reads standard input once, and every later read of it in the same
/// command line (a second <c>@-</c>, <c>-K -</c> then <c>@-</c>, or <c>@-</c> across <c>--next</c>)
/// gets end of file, which is empty data, instead of failing on a closed stream with exit 2.
/// </summary>
[TestClass]
public sealed class Cgr017Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReadStandardInput_ReadTwiceFromOneProcessStream_SecondReadIsEmpty()
    {
        using MemoryStream processStandardInput = new([0x61, 0x62, 0x63, 0x0A]);
        Diagnostics.Arrange("standard input", Convert.ToHexString(processStandardInput.ToArray()));
        DiskDataFileReader reader = new(_ => [], () => processStandardInput, path => throw new FileNotFoundException(null, path), true);

        byte[] first = reader.ReadStandardInput();
        byte[] second = reader.ReadStandardInput();
        Diagnostics.Bytes("first read", first);
        Diagnostics.Bytes("second read", second);

        Diagnostics.Diff("first read", new byte[] { 0x61, 0x62, 0x63, 0x0A }, first);
        Diagnostics.Assert("second read length", 0, second.Length);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63, 0x0A }, first);
        Assert.IsEmpty(second);
    }
}
