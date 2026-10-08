using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpAuthUsingLines" />: the <c>Server auth using</c> and <c>Proxy auth
/// using</c> lines curl 8.21.0 writes before a request, and when it writes none (measured,
/// BL-954 Notes).
/// </summary>
[TestClass]
public sealed class HttpAuthUsingLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Basic dTpw", false, HttpAuthSchemes.Basic, false, "Server auth using Basic with user 'u'", DisplayName = "-u u:p")]
    [DataRow("Basic dTpw", false, HttpAuthSchemes.Basic, true, null, DisplayName = "-u u:p with -H Authorization")]
    [DataRow("Bearer tok", false, HttpAuthSchemes.Basic, false, "Server auth using Bearer with user 'u'", DisplayName = "--oauth2-bearer tok -u u:p")]
    [DataRow("Bearer tok", false, HttpAuthSchemes.Basic, true, null, DisplayName = "--oauth2-bearer with -H Authorization")]
    [DataRow(null, false, HttpAuthSchemes.Digest, false, "Server auth using Digest with user 'u'", DisplayName = "--digest, first request")]
    [DataRow("Digest username=\"u\"", true, HttpAuthSchemes.Digest, true, "Server auth using Digest with user 'u'", DisplayName = "--digest answer with -H Authorization")]
    [DataRow(null, true, HttpAuthSchemes.Digest, false, null, DisplayName = "--digest, no answer to a challenge")]
    [DataRow(null, false, HttpAuthSchemes.Any, false, null, DisplayName = "--anyauth, first request")]
    [DataRow("NTLM TlRM", false, HttpAuthSchemes.Ntlm, false, "Server auth using NTLM with user 'u'", DisplayName = "--ntlm")]
    [DataRow("Negotiate YII=", false, HttpAuthSchemes.Negotiate, false, "Server auth using Negotiate with user 'u'", DisplayName = "--negotiate")]
    [DataRow("Other x", false, HttpAuthSchemes.Any, false, null, DisplayName = "Another scheme's value")]
    public void AuthUsing_OriginRequest_WritesCurlsLine(string? authorization, bool answersChallenge, HttpAuthSchemes allowed, bool headerNamesAuthorization, string? expected)
    {
        Diagnostics.Arrange("authorization, allowed", $"{authorization ?? "none"}, {allowed}");
        string? line = HttpAuthUsingLines.AuthUsing(Request(allowed, new NetworkCredential("u", "p")), authorization, answersChallenge, headerNamesAuthorization);
        Diagnostics.Act("line", line ?? "none");
        Diagnostics.Assert("line", expected ?? "none", line ?? "none");
        Assert.AreEqual(expected, line);
    }

    [TestMethod]
    public void AuthUsing_DigestWithoutUser_WritesNoLine()
    {
        Diagnostics.Arrange("allowed, credential", "Digest, none");
        string? line = HttpAuthUsingLines.AuthUsing(Request(HttpAuthSchemes.Digest, credential: null), null, answersChallenge: false, headerNamesAuthorization: false);
        Diagnostics.Act("line", line ?? "none");
        Diagnostics.Assert("line", "none", line ?? "none");
        Assert.IsNull(line);
    }

    [TestMethod]
    public void AuthUsing_BearerWithoutUser_NamesNoUser()
    {
        Diagnostics.Arrange("authorization, credential", "Bearer tok, none");
        string? line = HttpAuthUsingLines.AuthUsing(Request(HttpAuthSchemes.Basic, credential: null), "Bearer tok", answersChallenge: false, headerNamesAuthorization: false);
        Diagnostics.Act("line", line ?? "none");
        Diagnostics.Assert("line", "Server auth using Bearer with user ''", line);
        Assert.AreEqual("Server auth using Bearer with user ''", line);
    }

    [TestMethod]
    public void AuthUsing_SignedRequest_LeavesTheLineToTheSigner()
    {
        HttpAuthRequest request = Request(HttpAuthSchemes.Basic, new NetworkCredential("ak", "sk")) with { AwsSigV4 = new AwsSigV4Inputs("aws:amz:us-east-1:s3", "127.0.0.1", []) };

        Diagnostics.Arrange("authorization, AWS SigV4 provider", "AWS4-HMAC-SHA256 Credential=ak, aws:amz:us-east-1:s3");
        string? line = HttpAuthUsingLines.AuthUsing(request, "AWS4-HMAC-SHA256 Credential=ak", answersChallenge: false, headerNamesAuthorization: false);
        Diagnostics.Act("line", line ?? "none");
        Diagnostics.Assert("line", "none", line ?? "none");
        Assert.IsNull(line);
    }

    [TestMethod]
    [DataRow("Basic cHU6cHA=", HttpAuthSchemes.Basic, true, "Proxy auth using Basic with user 'pu'", DisplayName = "-U pu:pp, -H Authorization does not stop it")]
    [DataRow(null, HttpAuthSchemes.Digest, false, "Proxy auth using Digest with user 'pu'", DisplayName = "--proxy-digest, first request")]
    [DataRow("NTLM TlRM", HttpAuthSchemes.Ntlm, false, "Proxy auth using NTLM with user 'pu'", DisplayName = "--proxy-ntlm")]
    public void AuthUsing_ProxyRequest_WritesCurlsLine(string? authorization, HttpAuthSchemes allowed, bool headerNamesAuthorization, string expected)
    {
        HttpAuthRequest request = Request(allowed, new NetworkCredential("pu", "pp")) with { IsProxy = true };

        Diagnostics.Arrange("proxy authorization, allowed", $"{authorization ?? "none"}, {allowed}");
        string? line = HttpAuthUsingLines.AuthUsing(request, authorization, answersChallenge: false, headerNamesAuthorization);
        Diagnostics.Act("line", line ?? "none");
        Diagnostics.Assert("line", expected, line);
        Assert.AreEqual(expected, line);
    }

    private static HttpAuthRequest Request(HttpAuthSchemes allowed, NetworkCredential? credential) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", credential, null, allowed, IsProxy: false);
}
