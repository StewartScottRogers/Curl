using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the <c>Host</c> line Curl sends for a sample of each UTS #46 mapping class BL-4033 measured
/// (BL-4078): curl 8.18.0 with libidn2 2.3.8 on Linux in a UTF-8 locale sent each host below, and
/// <c>CurlUrlHostNameMappingTests</c> in Curl.Protocol.Abstractions.UnitTests pins every measured row
/// against the conversion alone. Off Windows only: Windows reads the host as Windows-1252 first
/// (CGR-059), and no Windows curl was measured for these hosts.
/// </summary>
[TestClass]
public sealed class Uts46MeasuredHostHeaderTests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ꭰ.invalid", "xn--58d.invalid")]
    [DataRow("ꭱ.invalid", "xn--68d.invalid")]
    [DataRow("ꭲ.invalid", "xn--78d.invalid")]
    public void Format_MeasuredCaseMapping_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ſ.invalid", "s.invalid")]
    [DataRow("ℌ.invalid", "h.invalid")]
    [DataRow("①.invalid", "1.invalid")]
    public void Format_MeasuredCompatibilityCharacter_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ｦ.invalid", "xn--mek.invalid")]
    [DataRow("ｧ.invalid", "xn--bck.invalid")]
    [DataRow("ｨ.invalid", "xn--dck.invalid")]
    public void Format_MeasuredEastAsianCompatibilityCharacter_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("가.invalid", "xn--o39a.invalid")]
    [DataRow("까.invalid", "xn--hl0b.invalid")]
    [DataRow("나.invalid", "xn--910b.invalid")]
    public void Format_MeasuredHangulJamo_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("\U0001D400.invalid", "a.invalid")]
    [DataRow("\U0001D401.invalid", "b.invalid")]
    [DataRow("\U0001D402.invalid", "c.invalid")]
    public void Format_MeasuredCharacterOutsideTheBmp_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("क़.invalid", "xn--11b2f.invalid")]
    [DataRow("ख़.invalid", "xn--21b0f.invalid")]
    [DataRow("ग़.invalid", "xn--31b8e.invalid")]
    public void Format_MeasuredIndicNukta_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ﭐ.invalid", "xn--qib.invalid")]
    [DataRow("ﭑ.invalid", "xn--qib.invalid")]
    [DataRow("ﭒ.invalid", "xn--0ib.invalid")]
    public void Format_MeasuredPresentationForm_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ạ̛.invalid", "xn--cta502l.invalid")]
    [DataRow("ȧ̱.invalid", "xn--dma64c.invalid")]
    [DataRow("â̖.invalid", "xn--2ca24i.invalid")]
    public void Format_MeasuredCombiningMarkOrder_SendsTheHostCurlSends(string host, string expected) =>
        AssertHostLine(host, expected);

    private static void AssertHostLine(string host, string expected)
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse($"http://{host}/"), new HttpRequestOptions());

        StringAssert.Contains(Encoding.Latin1.GetString(head), $"\r\nHost: {expected}\r\n");
    }
}
