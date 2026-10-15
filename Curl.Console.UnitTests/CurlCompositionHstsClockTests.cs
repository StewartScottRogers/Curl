using Curl.Engine;
namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlComposition.HstsClockOf" />, the HSTS clock <c>CURL_TIME</c> fixes behind the
/// harness seam (ADR-0493, BL-2234), and <see cref="CurlComposition.LearnsHstsOverHttp" />, the
/// learning over plain <c>http</c> <c>CURL_HSTS_HTTP</c> turns on behind it (ADR-0494, BL-2250).
/// </summary>
[TestClass]
public sealed class CurlCompositionHstsClockTests
{
    [TestMethod]
    public void HstsClockOf_WholeSeconds_FixesTheClockAtThatInstant()
    {
        FixedWallClock? clock = CurlComposition.HstsClockOf("1728465947");

        Assert.IsNotNull(clock);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1728465947), clock.GetUtcNow());
    }

    [TestMethod]
    public void HstsClockOf_TheLastSecondADateHolds_FixesTheClockThere()
    {
        FixedWallClock? clock = CurlComposition.HstsClockOf("253402300799");

        Assert.IsNotNull(clock);
        Assert.AreEqual(DateTimeOffset.MaxValue.ToUnixTimeSeconds(), clock.GetUtcNow().ToUnixTimeSeconds());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("soon")]
    [DataRow("-1")]
    [DataRow("253402300800")]
    public void HstsClockOf_NoUsableSeconds_KeepsTheRunsClock(string? value)
    {
        FixedWallClock? clock = CurlComposition.HstsClockOf(value);

        Assert.IsNull(clock);
    }

    [TestMethod]
    [DataRow(true, "1", true)]
    [DataRow(true, "", true)]
    [DataRow(true, null, false)]
    [DataRow(false, "1", false)]
    public void LearnsHstsOverHttp_SeamAndVariable_LearnsOnlyWithTheSeamOnAndTheVariableSet(bool seamOn, string? value, bool expected)
    {
        bool learns = CurlComposition.LearnsHstsOverHttp(seamOn, name => name == "CURL_HSTS_HTTP" ? value : null);

        Assert.AreEqual(expected, learns);
    }
}
