using Libcurl.Misc.Printf;

namespace Libcurl.UnitTests;

/// <summary>CGR-260: the <c>curl_mprintf</c> family prints a negative-signed NaN as <c>nan</c>, not <c>-nan</c>.</summary>
[TestClass]
public sealed class Cgr260Tests
{
    private static readonly double NegativeNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000UL));

    [TestMethod]
    [DataRow((int)PrintfFlags.None, 0, "nan")]
    [DataRow((int)PrintfFlags.ShowSign, 0, "+nan")]
    [DataRow((int)PrintfFlags.None, 5, "  nan")]
    [DataRow((int)PrintfFlags.FloatE, 0, "nan")]
    [DataRow((int)PrintfFlags.FloatG, 0, "nan")]
    public void Format_NegativeSignedNaN_PrintsWithoutAMinusSign(int flags, int width, string expected)
    {
        Assert.AreEqual(expected, PrintfDoubleWriter.Format(NegativeNaN, (PrintfFlags)flags, width, 6));
    }

    [TestMethod]
    public void Format_NegativeInfinity_StillPrintsMinusInf()
    {
        Assert.AreEqual("-inf", PrintfDoubleWriter.Format(double.NegativeInfinity, PrintfFlags.None, 0, 6));
    }
}
