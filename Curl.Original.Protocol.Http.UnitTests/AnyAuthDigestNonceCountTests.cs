using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the nonce count of the first Digest answer: <c>nc=00000002</c> under <c>--anyauth</c>
/// and <c>nc=00000001</c> under <c>--digest</c>, as curl 8.22.0 sends them (BL-3897).
/// </summary>
[TestClass]
public sealed class AnyAuthDigestNonceCountTests
{
    private const string Challenge = "Digest realm=\"r\", nonce=\"n\", qop=\"auth\"";

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, "nc=00000002", DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Digest, "nc=00000001", DisplayName = "--digest")]
    public void CreateAuthorization_FirstChallenge_SendsTheNonceCountCurlSends(HttpAuthSchemes allowed, string expected)
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "cn");
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://127.0.0.1/a"), "/a", new NetworkCredential("user", "pass"), null, allowed, IsProxy: false);

        string? value = authenticator.CreateAuthorization(request, [Challenge]);

        StringAssert.Contains(value, expected);
    }
}
