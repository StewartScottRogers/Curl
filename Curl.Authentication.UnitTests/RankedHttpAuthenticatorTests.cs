using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="RankedHttpAuthenticator" /> to the scheme curl 8.21.0 (mingw) answered
/// when a loopback server offered several (BL-218 Notes), and to when NTLM answers and goes
/// on after a credential was sent (BL-526 Notes).
/// </summary>
[TestClass]
public sealed class RankedHttpAuthenticatorTests
{
    // The response curl sent for -u u:p, GET /a, realm "r", nonce "n", MD5, no qop.
    private const string MeasuredDigestResponse = "response=\"544c035f0f40d9ebf0295157d8041f8c\"";

    private const string Basic = "Basic realm=\"r\"";
    private const string Digest = "Digest realm=\"r\", nonce=\"n\"";

    private static readonly RankedHttpAuthenticator Authenticator = WithContexts(new ScriptedSecurityContextFactory());

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, Digest }, DisplayName = "--anyauth, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Basic realm=\"r\", Digest realm=\"r\", nonce=\"n\"" }, DisplayName = "--anyauth, both in one header")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, new[] { Basic, Digest }, DisplayName = "--basic --digest, Basic then Digest")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Digest }, DisplayName = "--digest, Digest")]
    public void CreateAuthorization_DigestRanksFirst_AnswersDigest(HttpAuthSchemes allowed, string[] challenges)
    {
        string? value = Authenticator.CreateAuthorization(Request(allowed), challenges);

        StringAssert.StartsWith(value, "Digest username=\"u\"");
        StringAssert.Contains(value, MeasuredDigestResponse);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth, Basic only")]
    [DataRow(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, DisplayName = "--basic --digest, Basic only")]
    public void CreateAuthorization_OnlyBasicOfferedAndAllowed_AnswersBasic(HttpAuthSchemes allowed)
    {
        Assert.AreEqual("Basic dTpw", Authenticator.CreateAuthorization(Request(allowed), [Basic]));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, "NTLM" }, DisplayName = "--anyauth, Basic and NTLM: NTLM picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "NTLM" }, DisplayName = "--anyauth, NTLM only")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Basic }, DisplayName = "--anyauth, Negotiate and Basic: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Negotiate", Digest }, DisplayName = "--anyauth, Negotiate and Digest: Negotiate picked")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Digest realm=\"r\"", Basic }, DisplayName = "--anyauth, Digest without nonce and Basic")]
    [DataRow(HttpAuthSchemes.Any, new[] { "Foo realm=\"r\"" }, DisplayName = "--anyauth, unknown scheme only")]
    [DataRow(HttpAuthSchemes.Digest, new[] { Basic }, DisplayName = "--digest, Basic only")]
    [DataRow(HttpAuthSchemes.Basic, new[] { Digest }, DisplayName = "--basic, Digest only")]
    public void CreateAuthorization_PickIsNotAnswerable_SendsNothingAndDoesNotFallBack(HttpAuthSchemes allowed, string[] challenges)
    {
        Assert.IsNull(Authenticator.CreateAuthorization(Request(allowed), challenges));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", DisplayName = "-u u:p")]
    [DataRow(HttpAuthSchemes.Any, null, DisplayName = "--anyauth")]
    [DataRow(HttpAuthSchemes.Digest, null, DisplayName = "--digest")]
    public void CreateAuthorization_BeforeAnyChallenge_AnswersAsCurlDoes(HttpAuthSchemes allowed, string? expected)
    {
        Assert.AreEqual(expected, Authenticator.CreateAuthorization(Request(allowed), []));
    }

    [TestMethod]
    public void CreateAuthorization_BearerOutranksDigest_AnswersBearer()
    {
        HttpAuthRequest request = Request(HttpAuthSchemes.Digest | HttpAuthSchemes.Bearer) with { BearerToken = "tok" };

        Assert.AreEqual("Bearer tok", Authenticator.CreateAuthorization(request, [Digest, "Bearer realm=\"r\""]));
    }

    [TestMethod]
    public void CreateAuthorization_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Any), null!));
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Negotiate, DisplayName = "--negotiate")]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth")]
    public async Task CreateAuthorizationAsync_NegotiatePickedWithCredential_AnswersNegotiate(HttpAuthSchemes allowed)
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01, 0x02])));

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), ["Negotiate", Basic], CancellationToken.None);

        Assert.AreEqual("Negotiate AQI=", value);
        Assert.AreEqual("127.0.0.1", contexts.Requests.Single().HostName);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiateAloneBeforeAnyChallenge_TriesNegotiateEvenWithoutCredential()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = null };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, [], CancellationToken.None);

        Assert.AreEqual("Negotiate AQ==", value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NoTicket_SendsNothingAsBothPlatformCurlsDo()
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.NoCredentials, [])));

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(HttpAuthSchemes.Negotiate), ["Negotiate"], CancellationToken.None);

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NegotiatePickedWithoutCredential_SendsNothing()
    {
        ScriptedSecurityContextFactory contexts = new();
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { Credential = null };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, ["Negotiate"], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any, DisplayName = "--anyauth, before any challenge")]
    [DataRow(HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic, DisplayName = "--negotiate --basic, before any challenge")]
    public async Task CreateAuthorizationAsync_SeveralSchemesBeforeAnyChallenge_TriesNoNegotiate(HttpAuthSchemes allowed)
    {
        ScriptedSecurityContextFactory contexts = new();

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), [], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_ProxyOfferingNegotiate_LeavesItToTheProxyTask()
    {
        ScriptedSecurityContextFactory contexts = new();
        HttpAuthRequest request = Request(HttpAuthSchemes.Negotiate) with { IsProxy = true };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, ["Negotiate"], CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_DigestPicked_AnswersAsTheSynchronousCallDoes()
    {
        string? value = await Authenticator.CreateAuthorizationAsync(Request(HttpAuthSchemes.Any), [Basic, Digest], CancellationToken.None);

        StringAssert.Contains(value, MeasuredDigestResponse);
    }

    [TestMethod]
    public void CreateAuthorization_NegotiatePicked_SendsNothingWithoutIo()
    {
        Assert.IsNull(Authenticator.CreateAuthorization(Request(HttpAuthSchemes.Negotiate), ["Negotiate"]));
    }

    [TestMethod]
    public async Task CreateAuthorizationAsync_NullArguments_Throw()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.CreateAuthorizationAsync(null!, [], CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.CreateAuthorizationAsync(Request(HttpAuthSchemes.Any), null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, new string[0], DisplayName = "--ntlm, before any challenge")]
    [DataRow(HttpAuthSchemes.Any, new[] { Basic, "NTLM" }, DisplayName = "--anyauth, Basic and NTLM: NTLM picked")]
    [DataRow(HttpAuthSchemes.Ntlm | HttpAuthSchemes.Basic, new[] { "NTLM" }, DisplayName = "--ntlm --basic, NTLM")]
    public async Task CreateAuthorizationAsync_NtlmAnswers_SendsType1(HttpAuthSchemes allowed, string[] challenges)
    {
        ScriptedSecurityContextFactory contexts = new(new ScriptedSecurityContext(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])));

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(Request(allowed), challenges, CancellationToken.None);

        Assert.AreEqual("NTLM AQ==", value);
        Assert.AreEqual(SecurityMechanism.Ntlm, contexts.Requests.Single().Mechanism);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm, new string[0], false, true, DisplayName = "--ntlm without -u, before any challenge")]
    [DataRow(HttpAuthSchemes.Any, new[] { "NTLM" }, false, true, DisplayName = "--anyauth without -u, NTLM")]
    [DataRow(HttpAuthSchemes.Ntlm, new[] { "NTLM" }, true, false, DisplayName = "Proxy offering NTLM: the proxy task's")]
    [DataRow(HttpAuthSchemes.Any, new string[0], false, false, DisplayName = "--anyauth, before any challenge")]
    public async Task CreateAuthorizationAsync_NtlmDoesNotAnswer_SendsNothing(HttpAuthSchemes allowed, string[] challenges, bool isProxy, bool withoutCredential)
    {
        ScriptedSecurityContextFactory contexts = new();
        HttpAuthRequest request = Request(allowed) with { IsProxy = isProxy, Credential = withoutCredential ? null : new NetworkCredential("u", "p") };

        string? value = await WithContexts(contexts).CreateAuthorizationAsync(request, challenges, CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_Type2AfterType1_SendsType3()
    {
        ScriptedSecurityContext context = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01]),
            new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));

        string? value = await WithContexts(new ScriptedSecurityContextFactory(context))
            .ContinueAuthorizationAsync(Request(HttpAuthSchemes.Ntlm), "NTLM AQ==", sentBeforeAnyChallenge: true, ["NTLM " + HandBuiltNtlmSecurityContextTests.MeasuredChallenge], CancellationToken.None);

        Assert.AreEqual("NTLM Aw==", value);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", new[] { Basic }, false, DisplayName = "Basic refused")]
    [DataRow(HttpAuthSchemes.Any, "Digest x", new[] { Digest, "NTLM" }, false, DisplayName = "Digest refused, Digest still the pick")]
    [DataRow(HttpAuthSchemes.Ntlm, "NTLM AQ==", new string[0], false, DisplayName = "No challenge")]
    [DataRow(HttpAuthSchemes.Ntlm, "NTLM AQ==", new[] { "NTLM" }, true, DisplayName = "A proxy's")]
    public async Task ContinueAuthorizationAsync_NotAnNtlmLeg_SendsNothing(HttpAuthSchemes allowed, string sent, string[] challenges, bool isProxy)
    {
        ScriptedSecurityContextFactory contexts = new();

        string? value = await WithContexts(contexts).ContinueAuthorizationAsync(Request(allowed) with { IsProxy = isProxy }, sent, sentBeforeAnyChallenge: true, challenges, CancellationToken.None);

        Assert.IsNull(value);
        Assert.IsEmpty(contexts.Requests);
    }

    [TestMethod]
    public async Task ContinueAuthorizationAsync_NullArguments_Throw()
    {
        HttpAuthRequest request = Request(HttpAuthSchemes.Ntlm);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(null!, "NTLM AQ==", true, ["NTLM"], CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(request, null!, true, ["NTLM"], CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Authenticator.ContinueAuthorizationAsync(request, "NTLM AQ==", true, null!, CancellationToken.None).AsTask());
    }

    private static RankedHttpAuthenticator WithContexts(ScriptedSecurityContextFactory contexts) => new(
        new BasicAndBearerAuthenticator(Encoding.UTF8),
        new DigestAuthenticator(Encoding.UTF8, () => "c"),
        new NegotiateHttpAuthenticator(contexts),
        new NtlmHttpAuthenticator(contexts, refusedChallengeFailsTransfer: false));

    private static HttpAuthRequest Request(HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1:18218/a"), "/a", new NetworkCredential("u", "p"), null, allowed, IsProxy: false);
}
