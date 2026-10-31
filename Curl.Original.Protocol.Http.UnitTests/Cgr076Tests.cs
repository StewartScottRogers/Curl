using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-076 (BL-3605): after a redirect to another host has withheld the user's
/// <c>-H</c> <c>Authorization</c> value (<see cref="HttpRequestOptions.AuthorizationHeaderWithheld" />),
/// the request carries no Basic value made from the Location's <c>x:y@</c>, as curl 8.22.0 sends
/// <c>GET /ok</c> to <c>localhost:18605</c> with no <c>Authorization</c> (BL-3605 Notes). A
/// Digest answer is still sent beside it, as <c>output_auth_headers</c> checks the custom
/// headers for Basic and Bearer alone.
/// </summary>
[TestClass]
public sealed class Cgr076Tests
{
    private const string Url = "http://localhost:18605/ok";

    private const string HostLine = "GET /ok HTTP/1.1\r\nHost: localhost:18605\r\n";

    private const string DefaultHeaders = "User-Agent: curl/8.22.0\r\nAccept: */*\r\n";

    [TestMethod]
    [DataRow("Basic eDp5", DisplayName = "Basic from the Location's user information")]
    [DataRow("Bearer xyz", DisplayName = "Bearer")]
    public void Format_AuthorizationHeaderWithheld_SendsNoGeneratedAuthorization(string authorization)
    {
        HttpRequestOptions options = new() { Headers = ["X-Custom: 1"], FollowedToAnotherHost = true, AuthorizationHeaderWithheld = true };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(Url), options, authorization: authorization);

        Assert.AreEqual(HostLine + DefaultHeaders + "X-Custom: 1\r\n\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_AuthorizationHeaderWithheld_StillSendsADigestAnswer()
    {
        HttpRequestOptions options = new() { AuthorizationHeaderWithheld = true };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(Url), options, authorization: "Digest username=\"x\"");

        Assert.AreEqual(HostLine + "Authorization: Digest username=\"x\"\r\n" + DefaultHeaders + "\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_NoAuthorizationHeaderWithheld_SendsTheBasicValue()
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(Url), new HttpRequestOptions(), authorization: "Basic eDp5");

        Assert.AreEqual(HostLine + "Authorization: Basic eDp5\r\n" + DefaultHeaders + "\r\n", Encoding.Latin1.GetString(head));
    }
}
