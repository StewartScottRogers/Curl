using System.Net;
using System.Net.Security;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SaslAuthenticator" />'s GSSAPI and NTLM (BL-538, ADR-0184): where they rank,
/// the context each asks for, the NTLM exchange curl 8.21.0 (Schannel) made with
/// <c>Record-CurlExchange.ps1 -Smtp</c> on 2026-09-29 (BL-538 Notes), and RFC 4752's GSSAPI
/// exchange with its security-layer message, which cannot be measured without a KDC.
/// </summary>
[TestClass]
public sealed class SaslAuthenticatorSecurityContextTests
{
    private const string Host = "127.0.0.1";

    public TestContext TestContext { get; set; } = null!;

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static readonly byte[] Type1 = Convert.FromBase64String("TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==");

    private static readonly byte[] Type2 = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredChallenge);

    private static readonly byte[] Type3 = Convert.FromBase64String(HandBuiltNtlmSecurityContextTests.MeasuredType3);

    private static readonly byte[] KerberosToken = [0x60, 0x01, 0x02];

    private static readonly string[] AllNine =
        ["EXTERNAL", "GSSAPI", "DIGEST-MD5", "CRAM-MD5", "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN"];

    [TestMethod]
    [DataRow(@"DOMAIN\u", "GSSAPI", DisplayName = @"-u DOMAIN\u:p: GSSAPI")]
    [DataRow("DOMAIN/u", "GSSAPI", DisplayName = "-u DOMAIN/u:p: GSSAPI")]
    [DataRow("u@REALM", "GSSAPI", DisplayName = "-u u@REALM:p: GSSAPI")]
    [DataRow("", "GSSAPI", DisplayName = "-u :p, the default credentials: GSSAPI")]
    [DataRow("u", "DIGEST-MD5", DisplayName = "-u u:p, no domain: DIGEST-MD5")]
    [DataRow(@"\u", "DIGEST-MD5", DisplayName = @"-u \u:p, separator first: DIGEST-MD5")]
    [DataRow("u@", "DIGEST-MD5", DisplayName = "-u u@:p, separator last: DIGEST-MD5")]
    public void ChooseMechanism_WithSecurityContexts_PicksGssapiOnlyForADomainUser(string user, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("password", "p");
        diagnostics.Arrange("offered", string.Join(",", AllNine));

        string? chosen = Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(new NetworkCredential(user, "p")), AllNine);

        diagnostics.Act("chosen mechanism", chosen);
        diagnostics.Assert("mechanism", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    [DataRow(new[] { "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "NTLM", DisplayName = "Without the MD5 mechanisms: NTLM")]
    [DataRow(new[] { "CRAM-MD5", "NTLM", "PLAIN" }, "CRAM-MD5", DisplayName = "CRAM-MD5 ranks above NTLM")]
    [DataRow(new[] { "GSSAPI", "NTLM", "PLAIN" }, "NTLM", DisplayName = "GSSAPI without a domain user: NTLM")]
    [DataRow(new[] { "GSSAPI", "PLAIN" }, "PLAIN", DisplayName = "GSSAPI without a domain user: PLAIN")]
    public void ChooseMechanism_WithSecurityContexts_RanksNtlmAsCurl(string[] offered, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", string.Join(",", offered));
        diagnostics.Arrange("credential", "u:p");

        string? chosen = Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(new NetworkCredential("u", "p")), offered);

        diagnostics.Act("chosen mechanism", chosen);
        diagnostics.Assert("mechanism", expected, chosen);
        Assert.AreEqual(expected, chosen);
    }

    [TestMethod]
    [DataRow(null, "tok", DisplayName = "A token and no user: OAUTHBEARER")]
    [DataRow(@"DOMAIN\u", "tok", DisplayName = "A domain user and a token: OAUTHBEARER")]
    public void ChooseMechanism_BearerToken_SkipsGssapiAndNtlm(string? user, string token)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("bearer token", token);
        NetworkCredential? credential = user is null ? null : new NetworkCredential(user, "p");

        string? chosen = Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(credential, bearerToken: token), AllNine);

        diagnostics.Act("chosen mechanism", chosen);
        diagnostics.Assert("mechanism", "OAUTHBEARER", chosen);
        Assert.AreEqual("OAUTHBEARER", chosen);
    }

    [TestMethod]
    public void ChooseMechanism_NoCredential_SkipsGssapiAndNtlm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "GSSAPI,NTLM");
        diagnostics.Arrange("credential", "none");

        string? chosen = Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(null), ["GSSAPI", "NTLM"]);

        diagnostics.Act("chosen mechanism", chosen);
        diagnostics.Assert("mechanism", null, chosen);
        Assert.IsNull(chosen);
    }

    [TestMethod]
    public void ChooseMechanism_WithoutSecurityContexts_TreatsGssapiAndNtlmAsNotOffered()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("offered", "GSSAPI,NTLM");
        diagnostics.Arrange("credential", @"DOMAIN\u:p");

        string? chosen = new SaslAuthenticator(Windows1252).ChooseMechanism(Request(new NetworkCredential(@"DOMAIN\u", "p")), ["GSSAPI", "NTLM"]);

        diagnostics.Act("chosen mechanism", chosen);
        diagnostics.Assert("mechanism", null, chosen);
        Assert.IsNull(chosen);
    }

    [TestMethod]
    [DataRow("GSSAPI")]
    [DataRow("NTLM")]
    public void Begin_WithoutSecurityContexts_Throws(string mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("credential", "u:p");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new SaslAuthenticator(Windows1252).Begin(mechanism, Request(new NetworkCredential("u", "p"))));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("param name", "mechanism", exception.ParamName!);
        Assert.AreEqual("mechanism", exception.ParamName);
    }

    [TestMethod]
    public void Begin_CramMd5WithSecurityContexts_MakesNoContext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "CRAM-MD5");
        diagnostics.Arrange("credential", "u:p");
        ScriptedSecurityContextFactory factory = new();

        ISaslExchange exchange = Authenticator(factory).Begin("CRAM-MD5", Request(new NetworkCredential("u", "p")));

        diagnostics.Act("exchange mechanism", exchange.Mechanism);
        diagnostics.Act("context requests", factory.Requests.Count);
        diagnostics.Assert("mechanism", "CRAM-MD5", exchange.Mechanism);
        Assert.AreEqual("CRAM-MD5", exchange.Mechanism);
        Assert.IsEmpty(factory.Requests);
    }

    [TestMethod]
    public async Task Begin_Ntlm_SendsType1ThenType3AsCurl()
    {
        // Measured: AUTH NTLM <Type 1> (or AUTH NTLM, 334, <Type 1>), 334 <Type 2>, <Type 3>, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "ntlm");
        diagnostics.Arrange("credential", @"DOMAIN\user:pass");
        diagnostics.Bytes("scripted Type 1", Type1);
        diagnostics.Bytes("scripted Type 3", Type3);
        diagnostics.Bytes("server Type 2", Type2);
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, Type1), new(SecurityContextStatus.Completed, Type3));
        ScriptedSecurityContextFactory factory = new(context);
        ISaslExchange exchange = Authenticator(factory).Begin("ntlm", Request(new NetworkCredential(@"DOMAIN\user", "pass")));

        byte[]? initial = await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? final = await exchange.RespondAsync(Type2, CancellationToken.None);

        diagnostics.Act("exchange mechanism", exchange.Mechanism);
        diagnostics.Bytes("initial response", initial);
        diagnostics.Bytes("final response", final);
        diagnostics.Assert("mechanism", "NTLM", exchange.Mechanism);
        diagnostics.Diff("initial response", Type1, initial);
        diagnostics.Diff("final response", Type3, final);
        Assert.AreEqual("NTLM", exchange.Mechanism);
        CollectionAssert.AreEqual(Type1, initial);
        CollectionAssert.AreEqual(Type3, final);
        Assert.IsTrue(context.IsDisposed, "Nothing follows Type 3.");
        Assert.IsNull(await exchange.RespondAsync(Type2, CancellationToken.None));
        CollectionAssert.AreEqual(Type2, context.IncomingTokens[1]);
        Assert.AreEqual(
            new SecurityContextRequest(SecurityMechanism.Ntlm, "smtp", Host) { UserName = "user", Password = "pass", Domain = "DOMAIN" },
            factory.Requests.Single());
    }

    [TestMethod]
    [DataRow("u", "", null, DisplayName = "-u u:: no domain")]
    [DataRow("u", "NETDOM", "NETDOM", DisplayName = "The credential's own domain")]
    public void Begin_NtlmUserWithoutDomain_AsksForTheCredentialsDomain(string user, string domain, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("user", user);
        diagnostics.Arrange("password", "p");
        diagnostics.Arrange("domain", domain);
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());

        Authenticator(factory).Begin("NTLM", Request(new NetworkCredential(user, "p", domain)));

        diagnostics.Act("requested domain", factory.Requests.Single().Domain);
        diagnostics.Act("requested user", factory.Requests.Single().UserName);
        diagnostics.Assert("domain", expected, factory.Requests.Single().Domain);
        diagnostics.Assert("user", user, factory.Requests.Single().UserName);
        Assert.AreEqual(expected, factory.Requests.Single().Domain);
        Assert.AreEqual(user, factory.Requests.Single().UserName);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "-u :p")]
    [DataRow(false, DisplayName = "No credential")]
    public void Begin_NtlmEmptyUser_AsksForTheDefaultCredentials(bool hasCredential)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("has credential", hasCredential);
        diagnostics.Arrange("service and host", "smtp " + Host);
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());

        Authenticator(factory).Begin("NTLM", Request(hasCredential ? new NetworkCredential(string.Empty, "p") : null));

        SecurityContextRequest expected = new(SecurityMechanism.Ntlm, "smtp", Host);
        diagnostics.Act("context request", factory.Requests.Single());
        diagnostics.Assert("context request", expected, factory.Requests.Single());
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "smtp", Host), factory.Requests.Single());
    }

    [TestMethod]
    public async Task Begin_NtlmContextCompletedByType1_AnswersNothingMore()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "NTLM");
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Bytes("scripted Type 1", Type1);
        diagnostics.Bytes("server Type 2", Type2);
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, Type1)) { IsCompleted = true };
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        byte[]? answer = await exchange.RespondAsync(Type2, CancellationToken.None);

        diagnostics.Act("answer", answer);
        diagnostics.Bytes("answer", answer);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsNull(answer);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow("NTLM", "u", DisplayName = "NTLM, no Type 1")]
    [DataRow("GSSAPI", @"DOMAIN\u", DisplayName = "GSSAPI, no first token (measured with no KDC)")]
    public async Task Begin_NoCredentialsForTheFirstToken_FailsWithExit94(string mechanism, string user)
    {
        // Measured 2026-09-29: curl 8.21.0 ends with (94) whatever it has sent (BL-856).
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Arrange("credential", user + ":p");
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin(mechanism, Request(new NetworkCredential(user, "p")));

        SaslAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<SaslAuthenticationFailedException>(
            async () => await exchange.GetInitialResponseAsync(CancellationToken.None));

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("exception message", failure.Message);
        diagnostics.Assert("exit code", CurlExitCode.AuthError, failure.ExitCode);
        diagnostics.Assert("message", "An authentication function returned an error", failure.Message);
        Assert.AreEqual(CurlExitCode.AuthError, failure.ExitCode);
        Assert.AreEqual("An authentication function returned an error", failure.Message);
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(await exchange.RespondAsync(Type2, CancellationToken.None));
    }

    [TestMethod]
    public async Task Begin_NtlmType2Refused_AnswersNullSoTheHandlerCancels()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "NTLM");
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Bytes("scripted Type 1", Type1);
        diagnostics.Bytes("server Type 2", "bad"u8);
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, Type1), new(SecurityContextStatus.MalformedToken, []));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        byte[]? answer = await exchange.RespondAsync("bad"u8.ToArray(), CancellationToken.None);

        diagnostics.Act("answer", answer);
        diagnostics.Bytes("answer", answer);
        diagnostics.Assert("context disposed", true, context.IsDisposed);
        Assert.IsNull(answer);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Begin_NtlmOnCurlsOwnNtlm_SendsCurlsType1AndAType3()
    {
        // Off Windows the router gives curl's own NTLM, whose Type 1 HandBuiltNtlmSecurityContextTests pins.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "NTLM");
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Bytes("server Type 2", Type2);
        ISaslExchange exchange = Authenticator(HandBuiltSecurityContextFactoryTests.Factory(new FakeKdc())).Begin("NTLM", Request(new NetworkCredential("u", "p")));

        byte[]? type1 = await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? type3 = await exchange.RespondAsync(Type2, CancellationToken.None);

        diagnostics.Act("Type 1", Convert.ToBase64String(type1!));
        diagnostics.Bytes("Type 3", type3);
        diagnostics.Assert("Type 1", HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String(type1!));
        Assert.AreEqual(HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String(type1!));
        AssertIsType3(type3);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Begin_NtlmOnSspi_SendsSspisType1AndAType3()
    {
        // Measured: SSPI's Type 1 starts NTLMSSP, type 1, flags 0xA2088207; the OS version after varies by build.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "NTLM");
        diagnostics.Arrange("credential", @"DOMAIN\user:pass");
        diagnostics.Bytes("server Type 2", Type2);
        ISaslExchange exchange = Authenticator(new SystemSecurityContextFactory()).Begin("NTLM", Request(new NetworkCredential(@"DOMAIN\user", "pass")));

        byte[]? type1 = await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? type3 = await exchange.RespondAsync(Type2, CancellationToken.None);

        diagnostics.Bytes("Type 1 prefix", type1![..16]);
        diagnostics.Bytes("Type 3", type3);
        diagnostics.Act("Type 3 length", type3?.Length);
        diagnostics.Diff("Type 1 prefix", Type1[..16], type1[..16]);
        CollectionAssert.AreEqual(Type1[..16], type1![..16]);
        AssertIsType3(type3);
    }

    [TestMethod]
    [DataRow(null, new byte[] { (byte)'S', 0x01, 0x00, 0x00, 0x00 }, DisplayName = "No --sasl-authzid: no layer, size 0")]
    [DataRow("z", new byte[] { (byte)'S', 0x01, 0x00, 0x00, 0x00, (byte)'z' }, DisplayName = "--sasl-authzid z: no layer, size 0, z")]
    public async Task Begin_GssapiWithMutualAuthentication_SendsTheTokenAnEmptyAnswerAndTheSecurityLayer(string? authorizationIdentity, byte[] expected)
    {
        // RFC 4752: AUTH GSSAPI <token>, 334 <AP-REP>, (empty), 334 <wrapped offer>, <wrapped choice>, 235.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "gssapi");
        diagnostics.Arrange("credential", @"EXAMPLE\alice:pw");
        diagnostics.Arrange("authorization identity", authorizationIdentity);
        diagnostics.Bytes("scripted Kerberos token", KerberosToken);
        diagnostics.Bytes("wrapped offer", WrappedOffer(0x07, 0x00, 0x10, 0x00));
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, KerberosToken), new(SecurityContextStatus.Completed, []));
        ScriptedSecurityContextFactory factory = new(context);
        ISaslExchange exchange = Authenticator(factory).Begin("gssapi", Request(new NetworkCredential(@"EXAMPLE\alice", "pw"), authorizationIdentity));

        byte[]? initial = await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? empty = await exchange.RespondAsync("AP-REP"u8.ToArray(), CancellationToken.None);
        context.IsCompleted = true;
        byte[]? choice = await exchange.RespondAsync(WrappedOffer(0x07, 0x00, 0x10, 0x00), CancellationToken.None);

        diagnostics.Act("exchange mechanism", exchange.Mechanism);
        diagnostics.Bytes("initial response", initial);
        diagnostics.Bytes("empty answer", empty);
        diagnostics.Bytes("security layer choice", choice);
        diagnostics.Assert("mechanism", "GSSAPI", exchange.Mechanism);
        diagnostics.Diff("initial response", KerberosToken, initial);
        diagnostics.Diff("empty answer", Array.Empty<byte>(), empty);
        diagnostics.Diff("security layer choice", expected, choice);
        Assert.AreEqual("GSSAPI", exchange.Mechanism);
        CollectionAssert.AreEqual(KerberosToken, initial);
        CollectionAssert.AreEqual(Array.Empty<byte>(), empty);
        CollectionAssert.AreEqual(expected, choice);
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(await exchange.RespondAsync(WrappedOffer(0x01, 0, 0, 0), CancellationToken.None));
        Assert.AreEqual(
            new SecurityContextRequest(SecurityMechanism.Kerberos, "smtp", Host) { UserName = "alice", Password = "pw", Domain = "EXAMPLE", MessageProtection = ProtectionLevel.Sign },
            factory.Requests.Single());
    }

    [TestMethod]
    [DataRow(SecurityDelegation.None, DisplayName = "--delegation none")]
    [DataRow(SecurityDelegation.Policy, DisplayName = "--delegation policy")]
    [DataRow(SecurityDelegation.Always, DisplayName = "--delegation always")]
    public void Begin_Gssapi_AsksTheContextForTheDelegationLevel(SecurityDelegation delegation)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("delegation", delegation);
        diagnostics.Arrange("credential", @"EXAMPLE\alice:pw");
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());
        SaslAuthenticator authenticator = new(Windows1252, factory) { GssapiDelegation = delegation };

        authenticator.Begin("GSSAPI", Request(new NetworkCredential(@"EXAMPLE\alice", "pw")));

        diagnostics.Act("requested delegation", factory.Requests.Single().Delegation);
        diagnostics.Act("requested mechanism", factory.Requests.Single().Mechanism);
        diagnostics.Assert("delegation", delegation, factory.Requests.Single().Delegation);
        diagnostics.Assert("mechanism", SecurityMechanism.Kerberos, factory.Requests.Single().Mechanism);
        Assert.AreEqual(delegation, factory.Requests.Single().Delegation);
        Assert.AreEqual(SecurityMechanism.Kerberos, factory.Requests.Single().Mechanism);
    }

    [TestMethod]
    public void Begin_NtlmWithDelegationAlways_NeverAsksTheContextToDelegate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("delegation", SecurityDelegation.Always);
        diagnostics.Arrange("credential", "alice:pw");
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());
        SaslAuthenticator authenticator = new(Windows1252, factory) { GssapiDelegation = SecurityDelegation.Always };

        authenticator.Begin("NTLM", Request(new NetworkCredential("alice", "pw")));

        diagnostics.Act("requested delegation", factory.Requests.Single().Delegation);
        diagnostics.Assert("delegation", SecurityDelegation.None, factory.Requests.Single().Delegation);
        Assert.AreEqual(SecurityDelegation.None, factory.Requests.Single().Delegation);
    }

    [TestMethod]
    public async Task Begin_GssapiCompletedOnTheFirstToken_AnswersTheOfferNext()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, KerberosToken)) { IsCompleted = true };
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "GSSAPI");
        diagnostics.Arrange("credential", "(default credentials)");
        diagnostics.Bytes("scripted Kerberos token", KerberosToken);
        diagnostics.Bytes("wrapped offer", WrappedOffer(0x01, 0xFF, 0xFF, 0xFF));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("GSSAPI", Request(new NetworkCredential("", "")));

        await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? choice = await exchange.RespondAsync(WrappedOffer(0x01, 0xFF, 0xFF, 0xFF), CancellationToken.None);

        diagnostics.Bytes("security layer choice", choice);
        diagnostics.Diff("security layer choice", new byte[] { (byte)'S', 0x01, 0, 0, 0 }, choice);
        diagnostics.Act("choice length", choice!.Length);
        CollectionAssert.AreEqual(new byte[] { (byte)'S', 0x01, 0, 0, 0 }, choice);
    }

    [TestMethod]
    [DataRow(true, new byte[] { (byte)'X', 0x06, 0x00, 0x10, 0x00 }, false, "GSSAPI handshake failure (invalid security layer)", DisplayName = "SSPI: only integrity and confidentiality offered")]
    [DataRow(true, new byte[] { (byte)'X', 0x02, 0x00, 0x00, 0x00 }, false, "GSSAPI handshake failure (invalid security layer)", DisplayName = "SSPI: 0x02 offered")]
    [DataRow(true, new byte[] { (byte)'X', 0x01, 0x00, 0x10 }, false, "GSSAPI handshake failure (invalid security data)", DisplayName = "SSPI: three bytes")]
    [DataRow(true, new byte[] { (byte)'X', 0x01, 0x00, 0x10, 0x00, 0x00 }, false, "GSSAPI handshake failure (invalid security data)", DisplayName = "SSPI: five bytes")]
    [DataRow(true, new byte[0], false, "GSSAPI handshake failure (empty security message)", DisplayName = "SSPI: empty")]
    [DataRow(true, new byte[] { (byte)'X', 0x01, 0x00, 0x10, 0x00 }, true, "GSSAPI handshake failure (decryption failed)", DisplayName = "SSPI: does not unwrap")]
    [DataRow(false, new byte[] { (byte)'X', 0x02, 0x00, 0x00, 0x00 }, false, "GSSAPI handshake failure (invalid security layer)", DisplayName = "GSS-API: 0x02 offered")]
    [DataRow(false, new byte[] { (byte)'X', 0x01, 0x00, 0x10 }, false, "GSSAPI handshake failure (invalid security data)", DisplayName = "GSS-API: three bytes")]
    [DataRow(false, new byte[0], false, "GSSAPI handshake failure (empty security message)", DisplayName = "GSS-API: empty")]
    [DataRow(false, new byte[] { (byte)'X', 0x01, 0x00, 0x10, 0x00 }, true, "gss_unwrap() failed: ", DisplayName = "GSS-API: does not unwrap")]
    public async Task Begin_GssapiOfferCurlRefuses_AnswersNullWithCurlsCancelLine(bool wordsFailuresAsSspi, byte[] wrappedOffer, bool unwrapFails, string expectedReason)
    {
        // curl 8.21.0's Curl_auth_create_gssapi_security_message: an infof line, then a cancel (BL-1336).
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, KerberosToken)) { IsCompleted = true, UnwrapFails = unwrapFails };
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "GSSAPI");
        diagnostics.Arrange("words failures as SSPI", wordsFailuresAsSspi);
        diagnostics.Arrange("unwrap fails", unwrapFails);
        diagnostics.Bytes("scripted Kerberos token", KerberosToken);
        diagnostics.Bytes("wrapped offer", wrappedOffer);
        ISaslExchange exchange = new SaslAuthenticator(Windows1252, new ScriptedSecurityContextFactory(context)) { WordsGssapiFailuresAsSspi = wordsFailuresAsSspi }
            .Begin("GSSAPI", Request(new NetworkCredential("", "")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);
        Assert.IsNull(exchange.CancelReason);

        byte[]? answer = await exchange.RespondAsync(wrappedOffer, CancellationToken.None);

        diagnostics.Act("answer", answer);
        diagnostics.Act("cancel reason", exchange.CancelReason);
        diagnostics.Assert("cancel reason", expectedReason, exchange.CancelReason);
        Assert.IsNull(answer);
        Assert.AreEqual(expectedReason, exchange.CancelReason);
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "SSPI wording")]
    [DataRow(false, DisplayName = "GSS-API wording")]
    public async Task Begin_GssapiGoodOffer_AnswersTheWrappedChoiceAndLeavesCancelReasonNull(bool wordsFailuresAsSspi)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, KerberosToken)) { IsCompleted = true };
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "GSSAPI");
        diagnostics.Arrange("words failures as SSPI", wordsFailuresAsSspi);
        diagnostics.Arrange("authorization identity", "z");
        diagnostics.Bytes("scripted Kerberos token", KerberosToken);
        diagnostics.Bytes("wrapped offer", WrappedOffer(0x01, 0, 0x10, 0));
        ISaslExchange exchange = new SaslAuthenticator(Windows1252, new ScriptedSecurityContextFactory(context)) { WordsGssapiFailuresAsSspi = wordsFailuresAsSspi }
            .Begin("GSSAPI", Request(new NetworkCredential("", ""), "z"));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        byte[]? choice = await exchange.RespondAsync(WrappedOffer(0x01, 0, 0x10, 0), CancellationToken.None);

        diagnostics.Bytes("security layer choice", choice);
        diagnostics.Act("cancel reason", exchange.CancelReason);
        diagnostics.Diff("security layer choice", new byte[] { (byte)'S', 0x01, 0, 0, 0, (byte)'z' }, choice);
        CollectionAssert.AreEqual(new byte[] { (byte)'S', 0x01, 0, 0, 0, (byte)'z' }, choice);
        Assert.IsNull(exchange.CancelReason);
    }

    [TestMethod]
    public async Task Begin_NtlmRefusedType2_NeverSetsCancelReason()
    {
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, Type1), new(SecurityContextStatus.Refused, []));
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "NTLM");
        diagnostics.Arrange("credential", "u:p");
        diagnostics.Bytes("scripted Type 1", Type1);
        diagnostics.Bytes("server Type 2", Type2);
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));

        byte[]? initial = await exchange.GetInitialResponseAsync(CancellationToken.None);
        byte[]? answer = await exchange.RespondAsync(Type2, CancellationToken.None);

        diagnostics.Bytes("initial response", initial);
        diagnostics.Act("answer", answer);
        diagnostics.Act("cancel reason", exchange.CancelReason);
        diagnostics.Diff("initial response", Type1, initial);
        CollectionAssert.AreEqual(Type1, initial);
        Assert.IsNull(answer);
        Assert.IsNull(exchange.CancelReason);
        Assert.IsNull(await exchange.RespondAsync(Type2, CancellationToken.None));
        Assert.IsNull(exchange.CancelReason);
    }

    [TestMethod]
    public async Task Begin_GssapiApReplyRefused_AnswersNull()
    {
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, KerberosToken), new(SecurityContextStatus.Refused, []));
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "GSSAPI");
        diagnostics.Arrange("credential", "(default credentials)");
        diagnostics.Bytes("scripted Kerberos token", KerberosToken);
        diagnostics.Bytes("server AP-REP", "bad"u8);
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("GSSAPI", Request(new NetworkCredential("", "")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        byte[]? answer = await exchange.RespondAsync("bad"u8.ToArray(), CancellationToken.None);

        diagnostics.Act("answer", answer);
        diagnostics.Bytes("answer", answer);
        diagnostics.Act("cancel reason", exchange.CancelReason);
        diagnostics.Assert("cancel reason", null, exchange.CancelReason);
        Assert.IsNull(answer);
        Assert.IsNull(exchange.CancelReason, "A refused context token fails the transfer with exit 67, not a cancel.");
        Assert.IsNull(await exchange.RespondAsync(WrappedOffer(0x01, 0, 0, 0), CancellationToken.None));
    }

    [TestMethod]
    public async Task Begin_GssapiOnTheHandBuiltKerberos_CompletesAgainstAnAcceptor()
    {
        // The hand-built route, as off Windows without a system GSS-API: a ticket for service/host
        // from the in-memory KDC, the AP-REP read back, and RFC 4121 wrap tokens both ways. The
        // KDC issues tickets for HTTP/server.example.test only, so --service-name HTTP names it.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", "GSSAPI");
        diagnostics.Arrange("service", "HTTP");
        diagnostics.Arrange("host", "server.example.test");
        diagnostics.Arrange("authorization identity", "z");
        FakeKdc kdc = new();
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        ISaslExchange exchange = Authenticator(HandBuiltSecurityContextFactoryTests.Factory(kdc))
            .Begin("GSSAPI", Request(new NetworkCredential("", ""), "z") with { ServiceName = "HTTP", Host = "server.example.test" });

        acceptor.Accept((await exchange.GetInitialResponseAsync(CancellationToken.None))!);
        CollectionAssert.AreEqual(Array.Empty<byte>(), await exchange.RespondAsync(acceptor.Reply(), CancellationToken.None));
        byte[]? answer = await exchange.RespondAsync(acceptor.Rfc4121Wrap([0x07, 0x00, 0x10, 0x00], acceptor.AcceptorSequence!.Value, encrypt: true), CancellationToken.None);

        (byte[] message, _, bool encrypted) = acceptor.Rfc4121Unwrap(answer!);
        diagnostics.Bytes("wrapped answer", answer);
        diagnostics.Bytes("unwrapped message", message);
        diagnostics.Act("encrypted", encrypted);
        diagnostics.Assert("ticket service name", "HTTP/server.example.test", string.Join("/", kdc.Requests.Single().Body.ServerName!.Components.ToArray()));
        diagnostics.Diff("unwrapped message", new byte[] { 0x01, 0x00, 0x00, 0x00, (byte)'z' }, message);
        CollectionAssert.AreEqual(new[] { "HTTP", "server.example.test" }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00, (byte)'z' }, message);
        Assert.IsFalse(encrypted, "curl wraps its choice without encryption.");
    }

    private static void AssertIsType3(byte[]? message)
    {
        Assert.IsNotNull(message);
        CollectionAssert.AreEqual("NTLMSSP\0"u8.ToArray(), message[..8]);
        Assert.AreEqual(3, message[8]);
    }

    // ScriptedSecurityContext unwraps by dropping the first byte.
    private static byte[] WrappedOffer(params byte[] offer) => [(byte)'X', .. offer];

    private static SaslAuthenticator Authenticator(ISecurityContextFactory factory) => new(Windows1252, factory);

    private static SaslRequest Request(NetworkCredential? credential, string? authorizationIdentity = null, string? bearerToken = null) =>
        new(credential, authorizationIdentity, bearerToken, RequiredMechanism: null, "smtp", Host);
}
