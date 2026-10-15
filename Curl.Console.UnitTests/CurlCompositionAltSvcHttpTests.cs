namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlComposition.UsesAltSvcOverHttp" />, the use of Alt-Svc over plain <c>http</c>
/// <c>CURL_ALTSVC_HTTP</c> turns on behind the harness seam (ADR-0497, BL-2263).
/// </summary>
[TestClass]
public sealed class CurlCompositionAltSvcHttpTests
{
    [TestMethod]
    [DataRow(true, "yeah", true)]
    [DataRow(true, "", true)]
    [DataRow(true, null, false)]
    [DataRow(false, "yeah", false)]
    public void UsesAltSvcOverHttp_SeamAndVariable_UsesItOnlyWithTheSeamOnAndTheVariableSet(bool seamOn, string? value, bool expected)
    {
        bool uses = CurlComposition.UsesAltSvcOverHttp(seamOn, name => name == "CURL_ALTSVC_HTTP" ? value : null);

        Assert.AreEqual(expected, uses);
    }
}
