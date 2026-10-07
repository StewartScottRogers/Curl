using Curl.Testing;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins <see cref="HstsExpiryText" /> against the dates curl 8.21.0 wrote on 2026-09-29 UTC
/// (BL-620's notes) and against years past 9999, which only its Linux and macOS builds write.
/// </summary>
[TestClass]
public sealed class HstsExpiryTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0L, "19700101 00:00:00")]
    [DataRow(1822197542L, "20270929 05:59:02")]
    [DataRow(1893455999L, "20291231 23:59:59")]
    [DataRow(951782400L, "20000229 00:00:00")]
    [DataRow(32535215997L, "30001231 23:59:57")]
    [DataRow(HstsCache.LatestWritableExpiryOnWindows, "30010101 20:59:59")]
    [DataRow(253402300800L, "100000101 00:00:00")]
    [DataRow(HstsCache.LatestWritableExpiryOffWindows, "21474855471231 23:59:59")]
    public void Format_Instant_WritesCurlsDate(long unixSeconds, string text)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("unix seconds", unixSeconds);

        string formatted = HstsExpiryText.Format(unixSeconds);

        diagnostics.Act("formatted", formatted);
        diagnostics.Assert("expiry text", text, formatted);
        Assert.AreEqual(text, formatted);
    }
}
