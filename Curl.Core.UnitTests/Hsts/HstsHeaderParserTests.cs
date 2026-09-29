namespace Curl.Core.Hsts;

/// <summary>
/// Pins how <see cref="HstsHeaderParser" /> reads a <c>Strict-Transport-Security</c> value, each
/// case measured against curl 8.21.0 (mingw, Schannel) on 2026-09-29 UTC by the HSTS file it
/// wrote after the header; the cases are in BL-620's notes.
/// </summary>
[TestClass]
public sealed class HstsHeaderParserTests
{
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
    public void Parse_ValueCurlReads_ReadsItsDirectives(string value, long maxAgeSeconds, bool includeSubDomains) =>
        Assert.AreEqual(new HstsHeader(maxAgeSeconds, includeSubDomains), HstsHeaderParser.Parse(value));

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
    public void Parse_ValueCurlRefuses_ReadsNothing(string value) =>
        Assert.IsNull(HstsHeaderParser.Parse(value));

    [TestMethod]
    public void Parse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HstsHeaderParser.Parse(null!));
}
