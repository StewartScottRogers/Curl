using System.Net;
using System.Net.Security;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

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
        Assert.AreEqual(expected, Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(new NetworkCredential(user, "p")), AllNine));
    }

    [TestMethod]
    [DataRow(new[] { "NTLM", "OAUTHBEARER", "XOAUTH2", "LOGIN", "PLAIN" }, "NTLM", DisplayName = "Without the MD5 mechanisms: NTLM")]
    [DataRow(new[] { "CRAM-MD5", "NTLM", "PLAIN" }, "CRAM-MD5", DisplayName = "CRAM-MD5 ranks above NTLM")]
    [DataRow(new[] { "GSSAPI", "NTLM", "PLAIN" }, "NTLM", DisplayName = "GSSAPI without a domain user: NTLM")]
    [DataRow(new[] { "GSSAPI", "PLAIN" }, "PLAIN", DisplayName = "GSSAPI without a domain user: PLAIN")]
    public void ChooseMechanism_WithSecurityContexts_RanksNtlmAsCurl(string[] offered, string expected)
    {
        Assert.AreEqual(expected, Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(new NetworkCredential("u", "p")), offered));
    }

    [TestMethod]
    [DataRow(null, "tok", DisplayName = "A token and no user: OAUTHBEARER")]
    [DataRow(@"DOMAIN\u", "tok", DisplayName = "A domain user and a token: OAUTHBEARER")]
    public void ChooseMechanism_BearerToken_SkipsGssapiAndNtlm(string? user, string token)
    {
        NetworkCredential? credential = user is null ? null : new NetworkCredential(user, "p");

        Assert.AreEqual("OAUTHBEARER", Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(credential, bearerToken: token), AllNine));
    }

    [TestMethod]
    public void ChooseMechanism_NoCredential_SkipsGssapiAndNtlm()
    {
        Assert.IsNull(Authenticator(new ScriptedSecurityContextFactory()).ChooseMechanism(Request(null), ["GSSAPI", "NTLM"]));
    }

    [TestMethod]
    public void ChooseMechanism_WithoutSecurityContexts_TreatsGssapiAndNtlmAsNotOffered()
    {
        Assert.IsNull(new SaslAuthenticator(Windows1252).ChooseMechanism(Request(new NetworkCredential(@"DOMAIN\u", "p")), ["GSSAPI", "NTLM"]));
    }

    [TestMethod]
    [DataRow("GSSAPI")]
    [DataRow("NTLM")]
    public void Begin_WithoutSecurityContexts_Throws(string mechanism)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new SaslAuthenticator(Windows1252).Begin(mechanism, Request(new NetworkCredential("u", "p"))));

        Assert.AreEqual("mechanism", exception.ParamName);
    }

    [TestMethod]
    public void Begin_CramMd5WithSecurityContexts_MakesNoContext()
    {
        ScriptedSecurityContextFactory factory = new();

        ISaslExchange exchange = Authenticator(factory).Begin("CRAM-MD5", Request(new NetworkCredential("u", "p")));

        Assert.AreEqual("CRAM-MD5", exchange.Mechanism);
        Assert.IsEmpty(factory.Requests);
    }

    [TestMethod]
    public async Task Begin_Ntlm_SendsType1ThenType3AsCurl()
    {
        // Measured: AUTH NTLM <Type 1> (or AUTH NTLM, 334, <Type 1>), 334 <Type 2>, <Type 3>, 235.
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, Type1), new(SecurityContextStatus.Completed, Type3));
        ScriptedSecurityContextFactory factory = new(context);
        ISaslExchange exchange = Authenticator(factory).Begin("ntlm", Request(new NetworkCredential(@"DOMAIN\user", "pass")));

        Assert.AreEqual("NTLM", exchange.Mechanism);
        CollectionAssert.AreEqual(Type1, await exchange.GetInitialResponseAsync(CancellationToken.None));
        CollectionAssert.AreEqual(Type3, await exchange.RespondAsync(Type2, CancellationToken.None));
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
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());

        Authenticator(factory).Begin("NTLM", Request(new NetworkCredential(user, "p", domain)));

        Assert.AreEqual(expected, factory.Requests.Single().Domain);
        Assert.AreEqual(user, factory.Requests.Single().UserName);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "-u :p")]
    [DataRow(false, DisplayName = "No credential")]
    public void Begin_NtlmEmptyUser_AsksForTheDefaultCredentials(bool hasCredential)
    {
        ScriptedSecurityContextFactory factory = new(new ScriptedSecurityContext());

        Authenticator(factory).Begin("NTLM", Request(hasCredential ? new NetworkCredential(string.Empty, "p") : null));

        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "smtp", Host), factory.Requests.Single());
    }

    [TestMethod]
    public async Task Begin_NtlmContextCompletedByType1_AnswersNothingMore()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, Type1)) { IsCompleted = true };
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        Assert.IsNull(await exchange.RespondAsync(Type2, CancellationToken.None));
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task Begin_NtlmNoCredentialsForType1_HasNoInitialResponseAndAnswersNothing()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));

        Assert.IsNull(await exchange.GetInitialResponseAsync(CancellationToken.None));
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(await exchange.RespondAsync(Type2, CancellationToken.None));
    }

    [TestMethod]
    public async Task Begin_NtlmType2Refused_AnswersNullSoTheHandlerCancels()
    {
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, Type1), new(SecurityContextStatus.MalformedToken, []));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("NTLM", Request(new NetworkCredential("u", "p")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        Assert.IsNull(await exchange.RespondAsync("bad"u8.ToArray(), CancellationToken.None));
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Begin_NtlmOnCurlsOwnNtlm_SendsCurlsType1AndAType3()
    {
        // Off Windows the router gives curl's own NTLM, whose Type 1 HandBuiltNtlmSecurityContextTests pins.
        ISaslExchange exchange = Authenticator(HandBuiltSecurityContextFactoryTests.Factory(new FakeKdc())).Begin("NTLM", Request(new NetworkCredential("u", "p")));

        Assert.AreEqual(HandBuiltNtlmSecurityContextTests.CurlType1, Convert.ToBase64String((await exchange.GetInitialResponseAsync(CancellationToken.None))!));
        AssertIsType3(await exchange.RespondAsync(Type2, CancellationToken.None));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Begin_NtlmOnSspi_SendsSspisType1AndAType3()
    {
        // Measured: SSPI's Type 1 starts NTLMSSP, type 1, flags 0xA2088207; the OS version after varies by build.
        ISaslExchange exchange = Authenticator(new SystemSecurityContextFactory()).Begin("NTLM", Request(new NetworkCredential(@"DOMAIN\user", "pass")));

        byte[]? type1 = await exchange.GetInitialResponseAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Type1[..16], type1![..16]);
        AssertIsType3(await exchange.RespondAsync(Type2, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(null, new byte[] { (byte)'S', 0x01, 0x00, 0x00, 0x00 }, DisplayName = "No --sasl-authzid: no layer, size 0")]
    [DataRow("z", new byte[] { (byte)'S', 0x01, 0x00, 0x00, 0x00, (byte)'z' }, DisplayName = "--sasl-authzid z: no layer, size 0, z")]
    public async Task Begin_GssapiWithMutualAuthentication_SendsTheTokenAnEmptyAnswerAndTheSecurityLayer(string? authorizationIdentity, byte[] expected)
    {
        // RFC 4752: AUTH GSSAPI <token>, 334 <AP-REP>, (empty), 334 <wrapped offer>, <wrapped choice>, 235.
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, KerberosToken), new(SecurityContextStatus.Completed, []));
        ScriptedSecurityContextFactory factory = new(context);
        ISaslExchange exchange = Authenticator(factory).Begin("gssapi", Request(new NetworkCredential(@"EXAMPLE\alice", "pw"), authorizationIdentity));

        Assert.AreEqual("GSSAPI", exchange.Mechanism);
        CollectionAssert.AreEqual(KerberosToken, await exchange.GetInitialResponseAsync(CancellationToken.None));
        CollectionAssert.AreEqual(Array.Empty<byte>(), await exchange.RespondAsync("AP-REP"u8.ToArray(), CancellationToken.None));
        context.IsCompleted = true;
        CollectionAssert.AreEqual(expected, await exchange.RespondAsync(WrappedOffer(0x07, 0x00, 0x10, 0x00), CancellationToken.None));
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(await exchange.RespondAsync(WrappedOffer(0x01, 0, 0, 0), CancellationToken.None));
        Assert.AreEqual(
            new SecurityContextRequest(SecurityMechanism.Kerberos, "smtp", Host) { UserName = "alice", Password = "pw", Domain = "EXAMPLE", MessageProtection = ProtectionLevel.Sign },
            factory.Requests.Single());
    }

    [TestMethod]
    public async Task Begin_GssapiCompletedOnTheFirstToken_AnswersTheOfferNext()
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, KerberosToken)) { IsCompleted = true };
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("GSSAPI", Request(new NetworkCredential("", "")));

        await exchange.GetInitialResponseAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { (byte)'S', 0x01, 0, 0, 0 }, await exchange.RespondAsync(WrappedOffer(0x01, 0xFF, 0xFF, 0xFF), CancellationToken.None));
    }

    [TestMethod]
    [DataRow(new byte[] { (byte)'X', 0x06, 0x00, 0x10, 0x00 }, DisplayName = "Only integrity and confidentiality offered")]
    [DataRow(new byte[] { (byte)'X', 0x01, 0x00, 0x10 }, DisplayName = "Three bytes")]
    [DataRow(new byte[] { (byte)'X', 0x01, 0x00, 0x10, 0x00, 0x00 }, DisplayName = "Five bytes")]
    [DataRow(new byte[0], DisplayName = "Does not unwrap")]
    public async Task Begin_GssapiOfferCurlRefuses_AnswersNull(byte[] wrappedOffer)
    {
        ScriptedSecurityContext context = new(new SecurityContextStep(SecurityContextStatus.Completed, KerberosToken)) { IsCompleted = true };
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("GSSAPI", Request(new NetworkCredential("", "")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        Assert.IsNull(await exchange.RespondAsync(wrappedOffer, CancellationToken.None));
        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public async Task Begin_GssapiApReplyRefused_AnswersNull()
    {
        ScriptedSecurityContext context = new(new(SecurityContextStatus.ContinueNeeded, KerberosToken), new(SecurityContextStatus.Refused, []));
        ISaslExchange exchange = Authenticator(new ScriptedSecurityContextFactory(context)).Begin("GSSAPI", Request(new NetworkCredential("", "")));
        await exchange.GetInitialResponseAsync(CancellationToken.None);

        Assert.IsNull(await exchange.RespondAsync("bad"u8.ToArray(), CancellationToken.None));
        Assert.IsNull(await exchange.RespondAsync(WrappedOffer(0x01, 0, 0, 0), CancellationToken.None));
    }

    [TestMethod]
    public async Task Begin_GssapiOnTheHandBuiltKerberos_CompletesAgainstAnAcceptor()
    {
        // The hand-built route, as off Windows without a system GSS-API: a ticket for service/host
        // from the in-memory KDC, the AP-REP read back, and RFC 4121 wrap tokens both ways. The
        // KDC issues tickets for HTTP/server.example.test only, so --service-name HTTP names it.
        FakeKdc kdc = new();
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        ISaslExchange exchange = Authenticator(HandBuiltSecurityContextFactoryTests.Factory(kdc))
            .Begin("GSSAPI", Request(new NetworkCredential("", ""), "z") with { ServiceName = "HTTP", Host = "server.example.test" });

        acceptor.Accept((await exchange.GetInitialResponseAsync(CancellationToken.None))!);
        CollectionAssert.AreEqual(Array.Empty<byte>(), await exchange.RespondAsync(acceptor.Reply(), CancellationToken.None));
        byte[]? answer = await exchange.RespondAsync(acceptor.Rfc4121Wrap([0x07, 0x00, 0x10, 0x00], acceptor.AcceptorSequence!.Value, encrypt: true), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "HTTP", "server.example.test" }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
        (byte[] message, _, bool encrypted) = acceptor.Rfc4121Unwrap(answer!);
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
