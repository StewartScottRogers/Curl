using Curl.Testing;

namespace Curl.Core.Hsts;

/// <summary>
/// Pins how <see cref="HstsHeaderParser" /> reads a <c>Strict-Transport-Security</c> value, each
/// case measured against curl 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by the HSTS file it
/// wrote after the header; the cases are in BL-620's notes.
/// </summary>
[TestClass]
public sealed class HstsHeaderParserTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("max-age=31536000; includeSubDomains", 31536000L, true)]
    [DataRow("max-age=\"60\"", 60L, false)]
    [DataRow("max-age = \"60\" ; includesubdomains", 60L, true)]
    [DataRow("MAX-AGE=60;INCLUDESUBDOMAINS", 60L, true)]
    [DataRow("foo=bar; max-age=60", 60L, false)]
    [DataRow("max-age=60x; includeSubDomains", 60L, true)]
    [DataRow("includeSubDomainsX; max-age=60", 60L, true)]
    [DataRow("max-age=60, includeSubDomains", 60L, false)]
    [DataRow("  max-age=60  ;  ;includeSubDomains  ", 60L, true)]
    [DataRow("\tmax-age=0060\t;", 60L, false)]
    [DataRow("max-age=0", 0L, false)]
    [DataRow("max-age=60; includeSubDomains\r\n", 60L, true)]
    [DataRow("max-age=9223372036854775807", long.MaxValue, false)]
    [DataRow("max-age=99999999999999999999999; includeSubDomains", long.MaxValue, true)]
    public void Parse_ValueCurlReads_ReadsItsDirectives(string value, long maxAgeSeconds, bool includeSubDomains)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header value", value);
        HstsHeader expected = new(maxAgeSeconds, includeSubDomains);

        HstsHeader? parsed = HstsHeaderParser.Parse(value);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("parsed header", expected, parsed);
        Assert.AreEqual(expected, parsed);
    }

    [TestMethod]
    [DataRow("max-age=60; max-age=70")]
    [DataRow("includeSubDomains")]
    [DataRow("max-age=")]
    [DataRow("max-age=\"60")]
    [DataRow("max-age=\"99999999999999999999999\"")]
    [DataRow("max-age=60; includeSubDomains; includeSubDomains")]
    [DataRow("max-age=-5")]
    [DataRow("max-age=+5")]
    [DataRow("max-age 60")]
    [DataRow("max-age")]
    [DataRow("")]
    public void Parse_ValueCurlRefuses_ReadsNothing(string value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header value", value);

        HstsHeader? parsed = HstsHeaderParser.Parse(value);

        diagnostics.Act("parsed", parsed?.ToString() ?? "null");
        diagnostics.Assert("parsed header", "null", parsed?.ToString() ?? "null");
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header value", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => HstsHeaderParser.Parse(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
