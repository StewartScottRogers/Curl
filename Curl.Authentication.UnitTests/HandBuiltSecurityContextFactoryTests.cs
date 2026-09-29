using System.Formats.Asn1;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

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

    private static readonly byte[] RandomBytes = [.. Enumerable.Range(1, 64).Select(value => (byte)value)];

    [TestMethod]
    public async Task Negotiate_TicketGrantingTicketCached_SendsMitNegTokenInitAndCompletesOnTheApReply()
    {
        FakeKdc kdc = new();
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(kdc).Create(Request(SecurityMechanism.Negotiate));

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, first.Status);
        Assert.IsFalse(context.IsCompleted);
        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88" }, kdc.Exchanges);
        CollectionAssert.AreEqual(new[] { "HTTP", Host }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
        Assert.AreEqual(KerberosServiceTicketSource.HostBasedServiceNameType, kdc.Requests.Single().Body.ServerName!.NameType);
        (string[] mechanisms, byte[] kerberosToken) = ReadNegTokenInit(first.Token);
        CollectionAssert.AreEqual(new[] { "1.2.840.113554.1.2.2" }, mechanisms);
        acceptor.Accept(kerberosToken);

        byte[] negTokenResp = new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode();
        SecurityContextStep second = await context.NextTokenAsync(negTokenResp, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsEmpty(second.Token);
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public async Task Kerberos_TicketGrantingTicketCached_SendsTheBareGssTokenAndCompletesOnTheApReply()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        acceptor.Accept(first.Token);
        SecurityContextStep second = await context.NextTokenAsync(acceptor.Reply(), CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, first.Status);
        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public async Task Negotiate_MixedCaseHostAndDomainRealm_AsksForTheLowerCasedPrincipalInThatRealm()
    {
        FakeKdc kdc = new();
        KerberosConfiguration configuration = Configuration("[domain_realm]\n .example.test = EXAMPLE.TEST\n");
        KerberosServiceTicketSource tickets = new(() => configuration, () => Cache(TicketGrantingTicket()), _ => Client(configuration, kdc));
        using ISecurityContext context = new HandBuiltSecurityContextFactory(tickets, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes), new FixedNtlmRandomSource(RandomBytes))
            .Create(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "Server.Example.Test"));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        CollectionAssert.AreEqual(new[] { "HTTP", Host }, kdc.Requests.Single().Body.ServerName!.Components.ToArray());
    }

    [TestMethod]
    public async Task Ntlm_MakesCurlsOwnNtlmType1()
    {
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Ntlm));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, step.Status);
        Assert.AreEqual("TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", Convert.ToBase64String(step.Token));
        Assert.IsInstanceOfType<HandBuiltNtlmSecurityContext>(context);
    }

    [TestMethod]
    public async Task FirstStep_NoCredentialCacheFile_AnswersNoCredentials()
    {
        await AssertFirstStepFailsAsync(
            new KerberosServiceTicketSource(() => KerberosConfiguration.Empty, () => throw new KerberosFileException(KerberosFileError.NotFound), _ => throw new AssertFailedException("No KDC client is needed.")),
            SecurityContextStatus.NoCredentials);
    }

    [TestMethod]
    public async Task FirstStep_CacheWithoutTicketGrantingTicket_AnswersNoCredentials()
    {
        FakeKdc kdc = new();

        await AssertFirstStepFailsAsync(Tickets(kdc, () => Cache()), SecurityContextStatus.NoCredentials);

        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task FirstStep_KdcUnreachable_AnswersRefused()
    {
        FakeKdc kdc = new();
        kdc.UnreachableHosts.Add("kdc.example.test");

        await AssertFirstStepFailsAsync(Tickets(kdc, () => Cache(TicketGrantingTicket())), SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task FirstStep_ExpiredTicketFromKdc_AnswersNoCredentials()
    {
        FakeKdc kdc = new() { Override = _ => FakeKdc.Error(32) };

        await AssertFirstStepFailsAsync(Tickets(kdc, () => Cache(TicketGrantingTicket())), SecurityContextStatus.NoCredentials);
    }

    [TestMethod]
    public async Task FirstStep_MalformedKrb5Conf_AnswersRefused()
    {
        await AssertFirstStepFailsAsync(
            new KerberosServiceTicketSource(() => throw new KerberosConfigurationException(KerberosConfigurationError.SectionSyntax, "bad"), () => Cache(), _ => throw new AssertFailedException("No KDC client is needed.")),
            SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task FirstStep_CachedServiceTicketOfAnUnknownEncryptionType_AnswersRefused()
    {
        CachedCredential desTicket = Cached(new KerberosPrincipal(KerberosServiceTicketSource.HostBasedServiceNameType, FakeKdc.Realm, ["HTTP", Host]), new KerberosKey(1, new byte[8]));

        await AssertFirstStepFailsAsync(Tickets(new FakeKdc(), () => Cache(desTicket)), SecurityContextStatus.Refused);
    }

    [TestMethod]
    public async Task Reply_NegTokenRespRejects_AnswersRefused()
    {
        byte[] reject = new SpnegoNegotiationResponse(SpnegoNegotiationState.Reject, null, null, null).Encode();

        Assert.AreEqual(SecurityContextStatus.Refused, await ReplyStatusAsync(SecurityMechanism.Negotiate, _ => reject));
    }

    [TestMethod]
    public async Task Reply_NegTokenRespWithoutResponseToken_AnswersRefused()
    {
        byte[] empty = new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, null, null).Encode();

        Assert.AreEqual(SecurityContextStatus.Refused, await ReplyStatusAsync(SecurityMechanism.Negotiate, _ => empty));
    }

    [TestMethod]
    public async Task Reply_NotANegTokenResp_AnswersMalformedToken()
    {
        Assert.AreEqual(SecurityContextStatus.MalformedToken, await ReplyStatusAsync(SecurityMechanism.Negotiate, _ => [0x30, 0x00]));
    }

    [TestMethod]
    public async Task Reply_KerberosTokenNotAnApReply_AnswersMalformedToken()
    {
        Assert.AreEqual(SecurityContextStatus.MalformedToken, await ReplyStatusAsync(SecurityMechanism.Kerberos, _ => [0x60, 0x00]));
    }

    [TestMethod]
    public async Task Reply_ApReplyInAnotherKey_AnswersRefused()
    {
        Assert.AreEqual(
            SecurityContextStatus.Refused,
            await ReplyStatusAsync(SecurityMechanism.Kerberos, acceptor => acceptor.Reply(Enumerable.Repeat((byte)0x44, 32).ToArray())));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WrapAndUnwrap_KerberosCompleted_ProtectMessagesWithTheContextKey(bool encrypt)
    {
        byte[] message = [0x01, 0x00, 0x10, 0x00];
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));
        acceptor.Accept((await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)).Token);
        await context.NextTokenAsync(acceptor.Reply(), CancellationToken.None);

        (byte[] wrappedMessage, ulong sequence, bool encrypted) = acceptor.Rfc4121Unwrap(context.Wrap(message, encrypt)!);
        byte[]? unwrapped = context.Unwrap(acceptor.Rfc4121Wrap(message, acceptor.AcceptorSequence!.Value, encrypt));

        CollectionAssert.AreEqual(message, wrappedMessage);
        Assert.AreEqual(acceptor.InitiatorSequence, sequence);
        Assert.AreEqual(encrypt, encrypted);
        CollectionAssert.AreEqual(message, unwrapped);
    }

    [TestMethod]
    public async Task Unwrap_AlteredWrapToken_AnswersNull()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Negotiate));
        (_, byte[] kerberosToken) = ReadNegTokenInit((await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None)).Token);
        acceptor.Accept(kerberosToken);
        await context.NextTokenAsync(new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode(), CancellationToken.None);
        byte[] wrapped = acceptor.Rfc4121Wrap([0x01, 0x02], acceptor.AcceptorSequence!.Value, encrypt: true);
        wrapped[^1] ^= 0xFF;

        byte[]? unwrapped = context.Unwrap(wrapped);

        Assert.IsNull(unwrapped);
    }

    [TestMethod]
    public async Task WrapAndUnwrap_BeforeTheContextCompletes_Throw()
    {
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Kerberos));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap([0x01], encrypt: true));

        await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap([0x01]));
    }

    [TestMethod]
    public void Dispose_BeforeAnyStep_DoesNotThrow()
    {
        ISecurityContext context = Factory(new FakeKdc()).Create(Request(SecurityMechanism.Negotiate));

        context.Dispose();

        Assert.IsFalse(context.IsCompleted);
    }

    [TestMethod]
    public void Create_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Factory(new FakeKdc()).Create(null!));
    }

    private static SecurityContextRequest Request(SecurityMechanism mechanism) => new(mechanism, "HTTP", Host);

    internal static HandBuiltSecurityContextFactory Factory(FakeKdc kdc) => Factory(Tickets(kdc, () => Cache(TicketGrantingTicket())));

    private static HandBuiltSecurityContextFactory Factory(KerberosServiceTicketSource tickets) =>
        new(tickets, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes), new FixedNtlmRandomSource(RandomBytes));

    private static KerberosServiceTicketSource Tickets(FakeKdc kdc, Func<CredentialCache> readCache)
    {
        KerberosConfiguration configuration = Configuration(string.Empty);
        return new KerberosServiceTicketSource(() => configuration, readCache, _ => Client(configuration, kdc));
    }

    private static KerberosConfiguration Configuration(string extra) =>
        new KerberosConfigurationStore(
            new InMemoryKerberosFiles().Add("/etc/krb5.conf", "[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n" + extra),
            _ => null).Read();

    private static KerberosKdcClient Client(KerberosConfiguration configuration, FakeKdc kdc) =>
        new(configuration, new FakeSrvLookup(), kdc, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes));

    private static CredentialCache Cache(params CachedCredential[] credentials) => new(null, FakeKdc.Alice, credentials);

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

    private static async Task AssertFirstStepFailsAsync(KerberosServiceTicketSource tickets, SecurityContextStatus expected)
    {
        using ISecurityContext context = Factory(tickets).Create(Request(SecurityMechanism.Negotiate));

        SecurityContextStep step = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(expected, step.Status);
        Assert.IsEmpty(step.Token);
        Assert.IsFalse(context.IsCompleted);
    }

    private static async Task<SecurityContextStatus> ReplyStatusAsync(SecurityMechanism mechanism, Func<FakeGssAcceptor, byte[]> reply)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(Request(mechanism));
        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        acceptor.Accept(mechanism == SecurityMechanism.Negotiate ? ReadNegTokenInit(first.Token).KerberosToken : first.Token);

        SecurityContextStep second = await context.NextTokenAsync(reply(acceptor), CancellationToken.None);

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
