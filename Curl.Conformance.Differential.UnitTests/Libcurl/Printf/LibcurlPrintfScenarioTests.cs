using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Printf;

[TestClass]
public sealed class LibcurlPrintfScenarioTests
{
    [TestMethod]
    [DataRow("[%12Lf]", true)]
    [DataRow("%Lf", true)]
    [DataRow("%#+12.2LE", true)]
    [DataRow("%1$Lg", true)]
    [DataRow("[%#10Ld]", false)]
    [DataRow("%f", false)]
    public void CrashesReference_Format_IsTrueOnlyForALongDoubleFloatConversion(string format, bool expected) =>
        Assert.AreEqual(expected, new LibcurlPrintfScenario(0, format, []).CrashesReference);
}
