using System.Formats.Asn1;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Runs ADR-0142's hand-built routes: NTLM, curl's own, whose exchange
/// <see cref="HandBuiltNtlmSecurityContextTests" /> pins; and Negotiate (SPNEGO over the hand-built Kerberos) against
/// <c>Curl.Kerberos.UnitTests</c>' in-memory KDC, reached through the KDC transport seam, and
/// its in-memory GSS-API acceptor: a TGS exchange from the cache's ticket-granting ticket, the
/// NegTokenInit MIT sends, the AP-REP read back, and each way it fails.
/// </summary>
[TestClass]
public sealed partial class HandBuiltSecurityContextFactoryTests
{
    private const string Host = "server.example.test";

    private const string DefaultCachePath = "/tmp/krb5cc_1000";

    private static readonly byte[] RandomBytes = [.. Enumerable.Range(1, 64).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Negotiate_TicketGrantingTicketCached_SendsMitNegTokenInitAndCompletesOnTheApReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("host", Host);
        using ISecurityContext context = Factory(kdc).Create(Request(SecurityMechanism.Negotiate));

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("first status", first.Status);
        diagnostics.Bytes("first token", first.Token);
        diagnostics.Act("KDC exchanges", string.Join(" | ", kdc.Exchanges));
        diagnostics.Act("requested service name", string.Join("/", kdc.Requests.Single().Body.ServerName!.Components.ToArray()));
        diagnostics.Assert("first status", SecurityContextStatus.ContinueNeeded, first.Status);
        diagnostics.Assert("is completed", false, context.IsCompleted);
        diagnostics.Diff("KDC exchanges", "udp kdc.example.test:88", string.Join(" | ", kdc.Exchanges));
        diagnostics.Diff("requested service name", "HTTP/" + Host, string.Join("/", kdc.Requests.Single().Body.ServerName!.Components.ToArray()));
        diagnostics.Assert("name type", KerberosServiceTicketSource.HostBasedServiceNameType, kdc.Requests.Single().Body.ServerName!.NameType);
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, first.Status);
        Assert.IsFalse(context.IsCompleted);
        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88" }, kdc.Exchanges);
        CollectionAssert.AreEqual(new[] { "HTTP", Host }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
        Assert.AreEqual(KerberosServiceTicketSource.HostBasedServiceNameType, kdc.Requests.Single().Body.ServerName!.NameType);
        (string[] mechanisms, byte[] kerberosToken) = ReadNegTokenInit(first.Token);
        diagnostics.Act("offered mechanisms", string.Join(" ", mechanisms));
        diagnostics.Diff("offered mechanisms", "1.2.840.113554.1.2.2", string.Join(" ", mechanisms));
        CollectionAssert.AreEqual(new[] { "1.2.840.113554.1.2.2" }, mechanisms);
        acceptor.Accept(kerberosToken);

        byte[] negTokenResp = new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode();
        diagnostics.Bytes("NegTokenResp", negTokenResp);
        SecurityContextStep second = await context.NextTokenAsync(negTokenResp, CancellationToken.None);

        diagnostics.Act("second status", second.Status);
        diagnostics.Bytes("second token", second.Token);
        diagnostics.Assert("second status", SecurityContextStatus.Completed, second.Status);
        diagnostics.Assert("second token length", 0, second.Token.Length);
        diagnostics.Assert("is completed", true, context.IsCompleted);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsEmpty(second.Token);
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public async Task Kerberos_TicketGrantingTicketCached_SendsTheBareGssTokenAndCompletesOnTheApReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        diagnostics.Arrange("mechanism", SecurityMechanism.Kerberos);
        diagnostics.Arrange("host", Host);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        diagnostics.Bytes("first token", first.Token);
        acceptor.Accept(first.Token);
        SecurityContextStep second = await context.NextTokenAsync(acceptor.Reply(), CancellationToken.None);

        diagnostics.Act("first status", first.Status);
        diagnostics.Act("second status", second.Status);
        diagnostics.Assert("first status", SecurityContextStatus.ContinueNeeded, first.Status);
        diagnostics.Assert("second status", SecurityContextStatus.Completed, second.Status);
        diagnostics.Assert("is completed", true, context.IsCompleted);
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, first.Status);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public async Task Negotiate_MixedCaseHostAndDomainRealm_AsksForTheLowerCasedPrincipalInThatRealm()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        diagnostics.Arrange("host", "Server.Example.Test");
        diagnostics.Arrange("domain_realm", ".example.test = EXAMPLE.TEST");
        KerberosConfiguration configuration = Configuration("[domain_realm]\n .example.test = EXAMPLE.TEST\n");
        KerberosServiceTicketSource tickets = new(() => configuration, () => Cache(TicketGrantingTicket()), _ => Client(configuration, kdc));
        using ISecurityContext context = new HandBuiltSecurityContextFactory(tickets, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes), new FixedNtlmRandomSource(RandomBytes))
            .Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "Server.Example.Test"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Act("requested service name", string.Join("/", kdc.Requests.Single().Body.ServerName!.Components.ToArray()));
        diagnostics.Assert("status", SecurityContextStatus.ContinueNeeded, step.Status);
        diagnostics.Diff("requested service name", "HTTP/" + Host, string.Join("/", kdc.Requests.Single().Body.ServerName!.Components.ToArray()));
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        CollectionAssert.AreEqual(new[] { "HTTP", Host }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
    }

    [TestMethod]
    public async Task GetAsync_TicketGrantingTicketCached_StoresTheTgsTicketInTheDefaultCacheSoTheSecondGetAsksNoKdc()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        InMemoryKerberosFiles files = CacheFiles(TicketGrantingTicket());
        int cacheLength = files.Contents(DefaultCachePath).Length;
        diagnostics.Arrange("host", Host);
        diagnostics.Arrange("cache length before", cacheLength);
        KerberosServiceTicketSource tickets = Tickets(kdc, () => Store(files));

        using KerberosCredential first = await tickets.GetAsync("HTTP", Host, CancellationToken.None);
        int storedLength = files.Contents(DefaultCachePath).Length;
        using KerberosCredential second = await tickets.GetAsync("HTTP", Host, CancellationToken.None);

        diagnostics.Act("cache length after first get", storedLength);
        diagnostics.Act("cache length after second get", files.Contents(DefaultCachePath).Length);
        diagnostics.Act("KDC exchange count", kdc.Exchanges.Count);
        diagnostics.Assert("KDC exchange count", 1, kdc.Exchanges.Count);
        diagnostics.Assert("cache length unchanged by second get", storedLength, files.Contents(DefaultCachePath).Length);
        diagnostics.Diff("second ticket", first.Ticket.Encode(), second.Ticket.Encode());
        diagnostics.Diff("server name", "HTTP/" + Host, string.Join("/", second.Server.Components.ToArray()));
        Assert.IsGreaterThan(cacheLength, storedLength);
        Assert.HasCount(1, kdc.Exchanges);
        Assert.AreEqual(storedLength, files.Contents(DefaultCachePath).Length);
        CollectionAssert.AreEqual(first.Ticket.Encode(), second.Ticket.Encode());
        CollectionAssert.AreEqual(new[] { "HTTP", Host }, second.Server.Components.ToArray());
    }

    [TestMethod]
    public async Task GetAsync_DomainRealmConfigured_ReadsTheCacheOnlyInTheKdcClient()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        InMemoryKerberosFiles files = CacheFiles(TicketGrantingTicket());
        diagnostics.Arrange("host", Host);
        diagnostics.Arrange("domain_realm", ".example.test = EXAMPLE.TEST");
        KerberosConfiguration configuration = Configuration("[domain_realm]\n .example.test = EXAMPLE.TEST\n");
        KerberosServiceTicketSource tickets = new(() => configuration, () => Store(files), _ => Client(configuration, kdc));

        using KerberosCredential ticket = await tickets.GetAsync("HTTP", Host, CancellationToken.None);

        diagnostics.Act("paths read", string.Join(" | ", files.PathsRead));
        diagnostics.Diff("paths read", DefaultCachePath, string.Join(" | ", files.PathsRead));
        CollectionAssert.AreEqual(new[] { DefaultCachePath }, files.PathsRead);
    }

    [TestMethod]
    public async Task Ntlm_MakesCurlsOwnNtlmType1()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Ntlm);
        diagnostics.Arrange("host", Host);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Ntlm));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("type 1 token", step.Token);
        diagnostics.Act("context type", context.GetType().Name);
        diagnostics.Assert("status", SecurityContextStatus.ContinueNeeded, step.Status);
        diagnostics.Diff("type 1 token base64", "TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", Convert.ToBase64String(step.Token));
        diagnostics.Assert("context type", nameof(HandBuiltNtlmSecurityContext), context.GetType().Name);
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual("TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", Convert.ToBase64String(step.Token));
        Assert.IsInstanceOfType<HandBuiltNtlmSecurityContext>(context);
    }

    [TestMethod]
    public async Task FirstStep_NoCredentialCacheFile_AnswersNoCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential cache", "file not found");
        await AssertFirstStepFailsAsync(
            diagnostics,
            new KerberosServiceTicketSource(() => KerberosConfiguration.Empty, () => throw new KerberosFileException(KerberosFileError.NotFound), _ => throw new AssertFailedException("No KDC client is needed.")),
            SecurityContextStatus.NoCredentials);
    }

    [TestMethod]
    public async Task FirstStep_CacheWithoutTicketGrantingTicket_AnswersNoCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        diagnostics.Arrange("credential cache", "no credentials");

        await AssertFirstStepFailsAsync(diagnostics, Tickets(kdc, () => Cache()), SecurityContextStatus.NoCredentials);

        diagnostics.Act("KDC exchange count", kdc.Exchanges.Count);
        diagnostics.Assert("KDC exchange count", 0, kdc.Exchanges.Count);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task FirstStep_KdcUnreachable_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new();
        kdc.UnreachableHosts.Add("kdc.example.test");
        diagnostics.Arrange("unreachable KDC", "kdc.example.test");

        await AssertFirstStepFailsAsync(diagnostics, Tickets(kdc, () => Cache(TicketGrantingTicket())), SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task FirstStep_ExpiredTicketFromKdc_AnswersNoCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeKdc kdc = new() { Override = _ => FakeKdc.Error(32) };
        diagnostics.Arrange("KDC error code", 32);

        await AssertFirstStepFailsAsync(diagnostics, Tickets(kdc, () => Cache(TicketGrantingTicket())), SecurityContextStatus.NoCredentials);
    }

    [TestMethod]
    public async Task FirstStep_MalformedKrb5Conf_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("krb5.conf", "section syntax error");
        await AssertFirstStepFailsAsync(
            diagnostics,
            new KerberosServiceTicketSource(() => throw new KerberosConfigurationException(KerberosConfigurationError.SectionSyntax, "bad"), () => Cache(), _ => throw new AssertFailedException("No KDC client is needed.")),
            SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task FirstStep_CachedServiceTicketOfAnUnknownEncryptionType_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        CachedCredential desTicket = Cached(new KerberosPrincipal(KerberosServiceTicketSource.HostBasedServiceNameType, FakeKdc.Realm, ["HTTP", Host]), new KerberosKey(1, new byte[8]));
        diagnostics.Arrange("cached ticket encryption type", 1);

        await AssertFirstStepFailsAsync(diagnostics, Tickets(new FakeKdc(), () => Cache(desTicket)), SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task Reply_NegTokenRespRejects_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] reject = new SpnegoNegotiationResponse(SpnegoNegotiationState.Reject, null, null, null).Encode();
        diagnostics.Bytes("reply", reject);

        SecurityContextStatus status = await ReplyStatusAsync(diagnostics, SecurityMechanism.Negotiate, _ => reject);

        diagnostics.Assert("status", SecurityContextStatus.Refused, status);
        Assert.AreEqual(SecurityContextStatus.Refused, status);
    }

    [TestMethod]
    public async Task Reply_NegTokenRespWithoutResponseToken_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] empty = new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, null, null).Encode();
        diagnostics.Bytes("reply", empty);

        SecurityContextStatus status = await ReplyStatusAsync(diagnostics, SecurityMechanism.Negotiate, _ => empty);

        diagnostics.Assert("status", SecurityContextStatus.Refused, status);
        Assert.AreEqual(SecurityContextStatus.Refused, status);
    }

    [TestMethod]
    public async Task Reply_NotANegTokenResp_AnswersMalformedToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("reply", [0x30, 0x00]);

        SecurityContextStatus status = await ReplyStatusAsync(diagnostics, SecurityMechanism.Negotiate, _ => [0x30, 0x00]);

        diagnostics.Assert("status", SecurityContextStatus.MalformedToken, status);
        Assert.AreEqual(SecurityContextStatus.MalformedToken, status);
    }

    [TestMethod]
    public async Task Reply_KerberosTokenNotAnApReply_AnswersMalformedToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("reply", [0x60, 0x00]);

        SecurityContextStatus status = await ReplyStatusAsync(diagnostics, SecurityMechanism.Kerberos, _ => [0x60, 0x00]);

        diagnostics.Assert("status", SecurityContextStatus.MalformedToken, status);
        Assert.AreEqual(SecurityContextStatus.MalformedToken, status);
    }

    [TestMethod]
    public async Task Reply_ApReplyInAnotherKey_AnswersRefused()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reply key", "32 bytes of 0x44");

        SecurityContextStatus status = await ReplyStatusAsync(diagnostics, SecurityMechanism.Kerberos, acceptor => acceptor.Reply(Enumerable.Repeat((byte)0x44, 32).ToArray()));

        diagnostics.Assert("status", SecurityContextStatus.Refused, status);
        Assert.AreEqual(SecurityContextStatus.Refused, status);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WrapAndUnwrap_KerberosCompleted_ProtectMessagesWithTheContextKey(bool encrypt)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] message = [0x01, 0x00, 0x10, 0x00];
        diagnostics.Arrange("encrypt", encrypt);
        diagnostics.Bytes("message", message);
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));
        acceptor.Accept((await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)).Token);
        await context.NextTokenAsync(acceptor.Reply(), CancellationToken.None);

        (byte[] wrappedMessage, ulong sequence, bool encrypted) = acceptor.Rfc4121Unwrap(context.Wrap(message, encrypt)!);
        byte[]? unwrapped = context.Unwrap(acceptor.Rfc4121Wrap(message, acceptor.AcceptorSequence!.Value, encrypt));

        diagnostics.Bytes("message the acceptor read", wrappedMessage);
        diagnostics.Act("sequence", sequence);
        diagnostics.Act("encrypted", encrypted);
        diagnostics.Bytes("message the context read", unwrapped!);
        diagnostics.Diff("wrapped message", message, wrappedMessage);
        diagnostics.Assert("sequence", acceptor.InitiatorSequence, sequence);
        diagnostics.Assert("encrypted", encrypt, encrypted);
        diagnostics.Diff("unwrapped message", message, unwrapped!);
        CollectionAssert.AreEqual(message, wrappedMessage);
        Assert.AreEqual(acceptor.InitiatorSequence, sequence);
        Assert.AreEqual(encrypt, encrypted);
        CollectionAssert.AreEqual(message, unwrapped);
    }

    [TestMethod]
    public async Task Unwrap_AlteredWrapToken_AnswersNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("alteration", "last byte of the wrap token XOR 0xFF");
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Negotiate));
        (_, byte[] kerberosToken) = ReadNegTokenInit((await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)).Token);
        acceptor.Accept(kerberosToken);
        await context.NextTokenAsync(new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode(), CancellationToken.None);
        byte[] wrapped = acceptor.Rfc4121Wrap([0x01, 0x02], acceptor.AcceptorSequence!.Value, encrypt: true);
        wrapped[^1] ^= 0xFF;
        diagnostics.Bytes("altered wrap token", wrapped);

        byte[]? unwrapped = context.Unwrap(wrapped);

        diagnostics.Act("unwrapped", unwrapped is null ? "null" : "bytes");
        diagnostics.Assert("unwrapped is null", true, unwrapped is null);
        Assert.IsNull(unwrapped);
    }

    [TestMethod]
    public async Task WrapAndUnwrap_BeforeTheContextCompletes_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Kerberos);
        diagnostics.Bytes("message", [0x01]);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));
        InvalidOperationException wrapFailure = Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap([0x01], encrypt: true));
        diagnostics.Act("wrap before any step", wrapFailure.GetType().Name);

        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        InvalidOperationException unwrapFailure = Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap([0x01]));
        diagnostics.Act("unwrap after the first step", unwrapFailure.GetType().Name);
        diagnostics.Assert("wrap exception", nameof(InvalidOperationException), wrapFailure.GetType().Name);
        diagnostics.Assert("unwrap exception", nameof(InvalidOperationException), unwrapFailure.GetType().Name);
    }

    [TestMethod]
    public void Dispose_BeforeAnyStep_DoesNotThrow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Negotiate));

        context.Dispose();

        diagnostics.Act("is completed", context.IsCompleted);
        diagnostics.Assert("is completed", false, context.IsCompleted);
        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request", "null");

        ArgumentNullException failure = Assert.ThrowsExactly<ArgumentNullException>(() => Factory(new FakeKdc()).Create(null!));

        diagnostics.Act("exception type", failure.GetType().Name);
        diagnostics.Act("parameter name", failure.ParamName);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), failure.GetType().Name);
    }

    private static SecurityContextRequest Request(SecurityMechanism mechanism) => new(mechanism, "HTTP", Host);

    internal static HandBuiltSecurityContextFactory Factory(FakeKdc kdc) => Factory(Tickets(kdc, () => Cache(TicketGrantingTicket())));

    private static HandBuiltSecurityContextFactory Factory(KerberosServiceTicketSource tickets) =>
        new(tickets, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes), new FixedNtlmRandomSource(RandomBytes));

    private static KerberosServiceTicketSource Tickets(FakeKdc kdc, Func<CredentialCacheStore> createCacheStore)
    {
        KerberosConfiguration configuration = Configuration(string.Empty);
        return new KerberosServiceTicketSource(() => configuration, createCacheStore, _ => Client(configuration, kdc));
    }

    private static KerberosConfiguration Configuration(string extra) =>
        new KerberosConfigurationStore(
            new InMemoryKerberosFiles().Add("/etc/krb5.conf", "[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n" + extra),
            _ => null).Read();

    private static KerberosKdcClient Client(KerberosConfiguration configuration, FakeKdc kdc) =>
        new(configuration, new FakeSrvLookup(), kdc, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes));

    /// <summary>A writable store whose default cache, <c>/tmp/krb5cc_1000</c>, holds <paramref name="credentials" />.</summary>
    private static CredentialCacheStore Cache(params CachedCredential[] credentials) => Store(CacheFiles(credentials));

    private static CredentialCacheStore Store(InMemoryKerberosFiles files) => new(files, _ => null, () => 1000, fileWriter: files);

    /// <summary>
    /// In-memory files holding the version 4 cache file <c>/tmp/krb5cc_1000</c> with the default
    /// principal <c>alice@EXAMPLE.TEST</c> and <paramref name="credentials" />.
    /// </summary>
    private static InMemoryKerberosFiles CacheFiles(params CachedCredential[] credentials) =>
        new InMemoryKerberosFiles { ReturnsCopies = true }.Add(
            DefaultCachePath,
            [
                0x05, 0x04, 0x00, 0x00,
                0, 0, 0, 1, 0, 0, 0, 1,
                0, 0, 0, 12, .. "EXAMPLE.TEST"u8,
                0, 0, 0, 5, .. "alice"u8,
                .. credentials.SelectMany(CredentialCacheWriter.WriteCredential),
            ]);

    private static CachedCredential TicketGrantingTicket() =>
        Cached(KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), new KerberosKey(18, [.. FakeKdc.TicketGrantingSessionKey]));

    private static CachedCredential Cached(KerberosPrincipal server, KerberosKey sessionKey, KerberosTicketFlags flags = KerberosTicketFlags.Forwardable) => new()
    {
        Client = FakeKdc.Alice,
        Server = server,
        SessionKey = sessionKey,
        AuthenticationTime = FakeKdc.Now.AddHours(-1),
        StartTime = DateTimeOffset.UnixEpoch,
        EndTime = FakeKdc.Now.AddHours(1),
        RenewUntil = DateTimeOffset.UnixEpoch,
        IsEncryptedInSessionKey = false,
        Flags = flags,
        Addresses = [],
        AuthorizationData = [],
        Ticket = FakeKdc.TicketFor(new KerberosPrincipalName(server.NameType, server.Components)).Encode(),
        SecondTicket = [],
    };

    private static async Task AssertFirstStepFailsAsync(TestDiagnostics diagnostics, KerberosServiceTicketSource tickets, SecurityContextStatus expected)
    {
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("expected status", expected);
        using ISecurityContext context = Factory(tickets).Create(Request(SecurityMechanism.Negotiate));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        diagnostics.Act("status", step.Status);
        diagnostics.Bytes("token", step.Token);
        diagnostics.Act("is completed", context.IsCompleted);
        diagnostics.Assert("status", expected, step.Status);
        diagnostics.Assert("token length", 0, step.Token.Length);
        diagnostics.Assert("is completed", false, context.IsCompleted);
        Assert.AreEqual(expected, step.Status);
        Assert.IsEmpty(step.Token);
        Assert.IsFalse(context.IsCompleted);
    }

    private static async Task<SecurityContextStatus> ReplyStatusAsync(TestDiagnostics diagnostics, SecurityMechanism mechanism, Func<FakeGssAcceptor, byte[]> reply)
    {
        diagnostics.Arrange("mechanism", mechanism);
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(mechanism));
        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        acceptor.Accept(mechanism == SecurityMechanism.Negotiate ? ReadNegTokenInit(first.Token).KerberosToken : first.Token);

        SecurityContextStep second = await context.NextTokenAsync(reply(acceptor), CancellationToken.None);

        diagnostics.Act("reply status", second.Status);
        diagnostics.Bytes("reply token", second.Token);
        diagnostics.Act("is completed", context.IsCompleted);
        diagnostics.Assert("reply token length", 0, second.Token.Length);
        diagnostics.Assert("is completed", false, context.IsCompleted);
        Assert.IsEmpty(second.Token);
        Assert.IsFalse(context.IsCompleted);
        return second.Status;
    }

    /// <summary>Reads a NegTokenInit (RFC 4178 section 4.2.1) as written from the RFC, not with the library's encoder.</summary>
    private static (string[] Mechanisms, byte[] KerberosToken) ReadNegTokenInit(byte[] token)
    {
        AsnReader framed = new(token, AsnEncodingRules.DER);
        AsnReader contents = framed.ReadSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true));
        Assert.AreEqual("1.3.6.1.5.5.2", contents.ReadObjectIdentifier());
        AsnReader fields = contents.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)).ReadSequence();
        AsnReader mechanismList = fields.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)).ReadSequence();
        List<string> mechanisms = [];
        while (mechanismList.HasData)
        {
            mechanisms.Add(mechanismList.ReadObjectIdentifier());
        }

        byte[] kerberosToken = fields.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 2, isConstructed: true)).ReadOctetString();
        Assert.IsFalse(fields.HasData, "MIT sends no reqFlags and no mechListMIC.");
        return ([.. mechanisms], kerberosToken);
    }
}
