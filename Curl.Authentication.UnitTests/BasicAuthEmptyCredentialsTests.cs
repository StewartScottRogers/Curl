using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="BasicAndBearerAuthenticator" /> for <c>-u</c> values with an empty part, as
/// curl 8.22.0 sent them to a loopback server (measured with <c>Record-CurlExchange.ps1</c>,
/// BL-3176 Notes): <c>-u :</c> sends no <c>Authorization</c> header, while <c>-u user:</c> sends
/// <c>Basic dXNlcjo=</c> and <c>-u :pass</c> sends <c>Basic OnBhc3M=</c>.
/// </summary>
[TestClass]
public sealed class BasicAuthEmptyCredentialsTests
{
    private static readonly BasicAndBearerAuthenticator Authenticator = new(Encoding.UTF8);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, DisplayName = "-u : to the origin")]
    [DataRow(true, DisplayName = "-U : to a proxy")]
    public void CreateAuthorization_EmptyUserAndEmptyPassword_GivesNoBasicValue(bool isProxy)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user, password, proxy", $"'', '', {isProxy}");

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential(string.Empty, string.Empty), isProxy), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", "(none)", value);
        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow("user", "", "Basic dXNlcjo=", DisplayName = "-u user: (curl 8.22.0: Basic dXNlcjo=)")]
    [DataRow("", "pass", "Basic OnBhc3M=", DisplayName = "-u :pass (curl 8.22.0: Basic OnBhc3M=)")]
    public void CreateAuthorization_OneEmptyPart_StillGivesTheMeasuredBasicValue(string user, string password, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user, password", $"'{user}', '{password}'");

        string? value = Authenticator.CreateAuthorization(Request(new NetworkCredential(user, password), isProxy: false), []);

        diagnostics.Act("Authorization", value);
        diagnostics.Assert("Authorization", expected, value);
        Assert.AreEqual(expected, value);
    }

    private static HttpAuthRequest Request(NetworkCredential credential, bool isProxy) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", credential, null, HttpAuthSchemes.Basic, isProxy);
}
