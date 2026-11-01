using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-066 (BL-3595): a <c>-H</c> value ending in a semicolon sends its name trimmed of
/// the white space around it, then a colon, as curl 8.22.0 sends (measured: <c>-H "X-E ;"</c>
/// and <c>-H " X-E;"</c> both send <c>X-E:</c>, <c>-H " ;"</c> sends <c>:</c>), while only an
/// untrimmed name overrides curl's own header (<c>-H "Accept ;"</c> sends <c>Accept: */*</c>
/// and then <c>Accept:</c>).
/// </summary>
[TestClass]
public sealed class Cgr066Tests
{
    [TestMethod]
    [DataRow("X-E ;", "X-E:")]
    [DataRow(" X-E;", "X-E:")]
    [DataRow("X-E\t;", "X-E:")]
    [DataRow("  X-E  ;", "X-E:")]
    [DataRow("X-E;", "X-E:")]
    [DataRow(" ;", ":")]
    public void Format_SemicolonForm_SendsTheTrimmedNameWithAnEmptyValue(string header, string sent)
    {
        string head = FormatWith(header);

        Assert.EndsWith("Accept: */*\r\n" + sent + "\r\n\r\n", head);
    }

    [TestMethod]
    [DataRow("X-E ; ")]
    [DataRow(";")]
    [DataRow("X-E;x")]
    public void Format_SemicolonNotLastOrNoName_SendsNothing(string header)
    {
        string head = FormatWith(header);

        Assert.EndsWith("Accept: */*\r\n\r\n", head);
    }

    [TestMethod]
    [DataRow("Accept ;")]
    [DataRow(" Accept;")]
    public void Format_PaddedName_DoesNotOverrideCurlsOwnHeader(string header)
    {
        string head = FormatWith(header);

        Assert.EndsWith("Accept: */*\r\nAccept:\r\n\r\n", head);
    }

    [TestMethod]
    public void Format_UnpaddedName_OverridesCurlsOwnHeader()
    {
        string head = FormatWith("Accept;");

        Assert.EndsWith("User-Agent: curl/8.22.0\r\nAccept:\r\n\r\n", head);
    }

    private static string FormatWith(string header) =>
        Encoding.Latin1.GetString(HttpRequestHeadFormatter.Format(
            CurlUrl.Parse("http://127.0.0.1:20335/a"),
            new HttpRequestOptions { Headers = [header], UserAgent = "curl/8.22.0" }));
}
