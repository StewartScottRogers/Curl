using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlDateParser.ParseOrEpoch" /> for CGR-047 (BL-3576, ADR-0821): curl 8.22.0 reads an
/// unreadable <c>Last-Modified</c> as the epoch and keeps the instant one second before the epoch as -1,
/// measured through <c>-R</c> on 2026-10-26.
/// </summary>
[TestClass]
public sealed class Cgr047Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("garbage", 0L, DisplayName = "unreadable")]
    [DataRow("Wed, 32 Oct 2015 07:28:00 GMT", 0L, DisplayName = "day 32")]
    [DataRow("Wed, 31 Dec 1969 23:59:59 GMT", -1L, DisplayName = "one second before the epoch")]
    [DataRow("Wed, 21 Oct 2015 07:28:00 GMT", 1_445_412_480L, DisplayName = "readable")]
    public void ParseOrEpoch_GivesCurlsDocumentTime(string text, long expected)
    {
        Diagnostics.Arrange("text", text);

        long unixSeconds = CurlDateParser.ParseOrEpoch(text);

        Diagnostics.Act("unix seconds", unixSeconds);
        Diagnostics.Assert("unix seconds", expected, unixSeconds);
        Assert.AreEqual(expected, unixSeconds);
    }

    [TestMethod]
    public void ParseOrEpoch_Null_Throws()
    {
        Diagnostics.Arrange("text", "(null)");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => CurlDateParser.ParseOrEpoch(null!));

        Diagnostics.Act("parameter", thrown.ParamName);
        Diagnostics.Assert("parameter", "text", thrown.ParamName);
        Assert.AreEqual("text", thrown.ParamName);
    }
}
