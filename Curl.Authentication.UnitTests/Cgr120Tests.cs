using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// CGR-120: curl's SSPI build fails <c>--ntlm</c> with exit 67, "Login denied", before
/// sending anything when SSPI will not acquire the credential for the Type 1 message, as for
/// an 1108-character user name (BL-3649); curl's own NTLM still sends nothing to fail on.
/// </summary>
[TestClass]
public sealed class Cgr120Tests
{
    [TestMethod]
    public async Task CreateAuthorizationAsync_SspiHasNoCredentialForType1_FailsLoginDenied()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));
        NtlmHttpAuthenticator authenticator = new(contexts, matchesSspiBuild: true);

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            () => authenticator.CreateAuthorizationAsync(Request(), null, sentBeforeAnyChallenge: false, [], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Login denied", failure.Message);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_SspiRefusesType1OtherwiseThanForCredentials_SendsNothing()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.Refused, [])));

        string? value = await new NtlmHttpAuthenticator(contexts, matchesSspiBuild: true).CreateAuthorizationAsync(Request(), null, sentBeforeAnyChallenge: false, [], CancellationToken.None);

        Assert.IsNull(value);
    }

    private static HttpAuthRequest Request() =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:21372/775"), "/775", new NetworkCredential("testuser" + new string('A', 1100), "testpass"), null, HttpAuthSchemes.Ntlm, IsProxy: false);
}
