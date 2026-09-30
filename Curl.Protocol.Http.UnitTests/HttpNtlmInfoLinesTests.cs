using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpNtlmInfoLines" />: which header of a 401, or of a 407 for a proxy's
/// handshake, curl writes NTLM's handshake lines before (measured, BL-848 Notes).
/// </summary>
[TestClass]
public sealed class HttpNtlmInfoLinesTests
{
    [TestMethod]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "NTLM", true, DisplayName = "Bare NTLM")]
    [DataRow(401, false, HttpAuthSchemes.Any, "www-authenticate", "ntlm TlRMTVNTUAAC", true, DisplayName = "Any case, with a message")]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "NTLM\tTlRMTVNTUAAC", true, DisplayName = "A tab after the scheme")]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "Basic realm=\"r\", \tNTLM", true, DisplayName = "Second of two, after blanks")]
    [DataRow(407, true, HttpAuthSchemes.Ntlm, "Proxy-Authenticate", "NTLM", true, DisplayName = "A proxy's 407")]
    [DataRow(407, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "NTLM", false, DisplayName = "Not a 401")]
    [DataRow(401, false, HttpAuthSchemes.Basic, "WWW-Authenticate", "NTLM", false, DisplayName = "NTLM not allowed")]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "Proxy-Authenticate", "NTLM", false, DisplayName = "Another header")]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "Basic realm=\"r\"", false, DisplayName = "Another scheme")]
    [DataRow(401, false, HttpAuthSchemes.Ntlm, "WWW-Authenticate", "NTLMX", false, DisplayName = "A longer scheme name")]
    public void IsNtlmChallenge_Header_DecidesWhetherTheLinesGoBeforeIt(int statusCode, bool isProxy, HttpAuthSchemes allowed, string name, string value, bool expected)
    {
        HttpStatusLine statusLine = HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X");
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", null, null, allowed, isProxy);

        Assert.AreEqual(expected, HttpNtlmInfoLines.IsNtlmChallenge(request, statusLine, new HttpResponseHeader(name, value)));
    }
}
