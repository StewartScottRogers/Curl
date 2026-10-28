using System.Text;

using Curl.Api.Options.Http;
using Curl.Cookies;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that a <c>-b</c> string holding an <c>argv</c> byte that is not valid UTF-8 (a lone surrogate
/// U+DC80 to U+DCFF, ADR-0909) goes out as that byte, as curl copies <c>argv</c> bytes unchanged (BL-3866).
/// </summary>
[TestClass]
public sealed class CookieEngineArgvByteTests
{
    private const string Url = "http://127.0.0.1:18231/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromHttpOptions_LoneSurrogateForAnArgvByte_SendsThatByte()
    {
        Diagnostics.Arrange("command line", "-b $'c=\xfe'");
        CookieEngine cookies = CookieEngine.FromHttpOptions(
            new EasyHttpOptions { Cookie = "c=\uDCFEé" }, new CookieStore(), Encoding.UTF8)!;

        string? cookieHeader = cookies.HandlerStore.GetCookieHeader(
            CurlUrl.Parse(Url), false, new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero), NoTransferEvents.Instance);
        Diagnostics.Act("cookie header", cookieHeader ?? "<null>");

        Diagnostics.Assert("cookie header", "c=þÃ©", cookieHeader ?? "<null>");
        Assert.AreEqual("c=þÃ©", cookieHeader);
    }
}
