namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins CGR-054 (BL-3583): curl 8.22.0 sends a URL path's <c>%XX</c> escapes with upper-case hex
/// digits (measured: <c>/a%2fb%7e</c> goes out as <c>GET /a%2Fb%7E</c>, <c>/a%zz%2/b%a</c> as typed),
/// which <see cref="UrlPathEscapes.WithUpperCaseHexDigits" /> gives the handlers.
/// </summary>
[TestClass]
public sealed class Cgr054Tests
{
    [TestMethod]
    [DataRow("/a%2fb%7e", "/a%2Fb%7E")]
    [DataRow("/%7euser/%e2%82%ac", "/%7Euser/%E2%82%AC")]
    [DataRow("/a%0d%0ab%2fc", "/a%0D%0Ab%2Fc")]
    [DataRow("/a%zz%2/b%a", "/a%zz%2/b%a")]
    [DataRow("/%%2f%", "/%%2F%")]
    [DataRow("/%2F", "/%2F")]
    public void WithUpperCaseHexDigits_PathWithEscapes_UpperCasesOnlyHexEscapes(string path, string expected)
    {
        Assert.AreEqual(expected, UrlPathEscapes.WithUpperCaseHexDigits(path));
    }

    [TestMethod]
    public void WithUpperCaseHexDigits_NoPercent_ReturnsThePathItself()
    {
        string path = "/plain/path";

        Assert.AreSame(path, UrlPathEscapes.WithUpperCaseHexDigits(path));
    }

    [TestMethod]
    public void WithUpperCaseHexDigits_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlPathEscapes.WithUpperCaseHexDigits(null!));
    }
}
