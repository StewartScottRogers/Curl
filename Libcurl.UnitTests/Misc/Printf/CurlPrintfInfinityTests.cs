using Libcurl.Misc.Printf;

namespace Libcurl.UnitTests.Misc.Printf;

// Infinity and NaN through every double conversion, width, precision and flag, as glibc's snprintf
// (which curl 8.22.0's out_double calls) prints them: precision and # change nothing, 0 pads with spaces.
// Each formats on a worker so a regression that loops forever fails at the timeout instead of hanging the run.
[TestClass]
public sealed class CurlPrintfInfinityTests
{
    private static readonly double PositiveNaN = BitConverter.UInt64BitsToDouble(0x7FF8_0000_0000_0000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    [DataRow("%f", "inf")]
    [DataRow("%.0f", "inf")]
    [DataRow("%.1f", "inf")]
    [DataRow("%.0F", "INF")]
    [DataRow("%5.1F", "  INF")]
    [DataRow("%e", "inf")]
    [DataRow("%.3E", "INF")]
    [DataRow("%g", "inf")]
    [DataRow("%.0G", "INF")]
    [DataRow("%6.2f", "   inf")]
    [DataRow("%-6.2f|", "inf   |")]
    [DataRow("%+.1f", "+inf")]
    [DataRow("% .1e", " inf")]
    [DataRow("%08.1f", "     inf")]
    [DataRow("%#.0f", "inf")]
    [DataRow("%#g", "inf")]
    [DataRow("%-+7.3g|", "+inf   |")]
    [DataRow("%400.1f", null)]
    public async Task MAPrintf_PositiveInfinity_PrintsInf(string format, string? expected)
    {
        Assert.AreEqual(expected ?? new string(' ', 322) + "inf", await FormatAsync(format, double.PositiveInfinity));
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    [DataRow("%f", "-inf")]
    [DataRow("%.0f", "-inf")]
    [DataRow("%.1f", "-inf")]
    [DataRow("%.0F", "-INF")]
    [DataRow("%5.1F", " -INF")]
    [DataRow("%e", "-inf")]
    [DataRow("%.3E", "-INF")]
    [DataRow("%g", "-inf")]
    [DataRow("%.0G", "-INF")]
    [DataRow("%6.2f", "  -inf")]
    [DataRow("%-6.2f|", "-inf  |")]
    [DataRow("%+.1f", "-inf")]
    [DataRow("% .1e", "-inf")]
    [DataRow("%08.1f", "    -inf")]
    [DataRow("%#.0f", "-inf")]
    [DataRow("%#g", "-inf")]
    [DataRow("%-+7.3g|", "-inf   |")]
    public async Task MAPrintf_NegativeInfinity_PrintsMinusInf(string format, string expected)
    {
        Assert.AreEqual(expected, await FormatAsync(format, double.NegativeInfinity));
    }

    // .NET's double.NaN has its sign bit set, which glibc prints as "-nan".
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    [DataRow("%f", "-nan")]
    [DataRow("%.0f", "-nan")]
    [DataRow("%.1f", "-nan")]
    [DataRow("%.0F", "-NAN")]
    [DataRow("%5.1F", " -NAN")]
    [DataRow("%e", "-nan")]
    [DataRow("%g", "-nan")]
    [DataRow("%-6.2f|", "-nan  |")]
    [DataRow("%08.1f", "    -nan")]
    [DataRow("%#g", "-nan")]
    public async Task MAPrintf_NaNWithItsSignBitSet_PrintsMinusNan(string format, string expected)
    {
        Assert.AreEqual(expected, await FormatAsync(format, double.NaN));
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    [DataRow("%f", "nan")]
    [DataRow("%.0f", "nan")]
    [DataRow("%.1f", "nan")]
    [DataRow("%.0F", "NAN")]
    [DataRow("%5.1F", "  NAN")]
    [DataRow("%e", "nan")]
    [DataRow("%g", "nan")]
    [DataRow("%-6.2f|", "nan   |")]
    [DataRow("%+.1f", "+nan")]
    [DataRow("% .1e", " nan")]
    [DataRow("%08.1f", "     nan")]
    [DataRow("%#g", "nan")]
    public async Task MAPrintf_NaNWithItsSignBitClear_PrintsNan(string format, string expected)
    {
        Assert.AreEqual(expected, await FormatAsync(format, PositiveNaN));
    }

    private async Task<string?> FormatAsync(string format, double value)
    {
        return await Task.Run(() => CurlPrintf.MAPrintf(format, value), TestContext.CancellationToken)
            .WaitAsync(TestContext.CancellationToken);
    }
}
