using System.Buffers.Binary;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

// <summary>
// Checks <c>--delegation</c> on the hand-built Kerberos route (BL-873): <c>always</c>, and
// <c>policy</c> with an ok-as-delegate service ticket, get a forwarded ticket-granting ticket
// from the KDC and send the delegation flag and a KRB-CRED in the authenticator's checksum,
// as MIT's gss_init_sec_context does; <c>none</c>, <c>policy</c> without ok-as-delegate, a
// ticket-granting ticket that is not forwardable, or any failure to forward send neither.
// </summary>
public sealed partial class HandBuiltSecurityContextFactoryTests
{
    private const uint DelegationFlag = 0x01;

    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public async Task Delegation_AlwaysWithForwardableTicketGrantingTicket_SendsTheDelegationFlagAndAKrbCred(SecurityMechanism mechanism)
    {
        FakeKdc kdc = new();

        FakeGssAcceptor acceptor = await EstablishAsync(SecurityDelegation.Always, Tickets(kdc, () => Cache(TicketGrantingTicket())), mechanism);

        Assert.HasCount(2, kdc.Requests);
        Assert.AreEqual(KerberosKdcOptions.Forwarded | KerberosKdcOptions.Forwardable, kdc.Requests[1].Body.Options);
        CollectionAssert.AreEqual(new[] { "krbtgt", FakeKdc.Realm }, kdc.Requests[1].Body.ServerName!.Components.ToArray());
        byte[] checksum = acceptor.Authenticator!.Checksum!.Value;
        Assert.AreEqual(DelegationFlag, ChecksumFlags(acceptor) & DelegationFlag);
        KerberosCredentialMessage credential = KerberosCredentialMessage.Decode(checksum.AsMemory(28));
        CollectionAssert.AreEqual(new[] { "krbtgt", FakeKdc.Realm }, credential.Tickets.Single().ServerName.Components.ToArray());
        using KerberosEncryptedCredentialPart part = KerberosEncryptedCredentialPart.Decode(acceptor.Encryption.Decrypt(acceptor.SessionKey, 14, credential.EncryptedPart.Cipher));
        Assert.IsTrue(part.Credentials.Single().Flags.HasFlag(KerberosTicketFlags.Forwarded));
    }

    [TestMethod]
    public async Task Delegation_PolicyWithOkAsDelegateServiceTicket_SendsTheDelegationFlag()
    {
        FakeKdc kdc = new();

        FakeGssAcceptor acceptor = await EstablishAsync(SecurityDelegation.Policy, Tickets(kdc, () => Cache(ServiceTicket(KerberosTicketFlags.OkAsDelegate), TicketGrantingTicket())));

        Assert.AreEqual(KerberosKdcOptions.Forwarded | KerberosKdcOptions.Forwardable, kdc.Requests.Single().Body.Options);
        Assert.AreEqual(DelegationFlag, ChecksumFlags(acceptor) & DelegationFlag);
    }

    [TestMethod]
    [DataRow(SecurityDelegation.None)]
    [DataRow(SecurityDelegation.Policy)]
    public async Task Delegation_NoneOrPolicyWithoutOkAsDelegate_SendsNoDelegationAndAsksForNoForwardedTicket(SecurityDelegation delegation)
    {
        FakeKdc kdc = new();

        FakeGssAcceptor acceptor = await EstablishAsync(delegation, Tickets(kdc, () => Cache(TicketGrantingTicket())));

        Assert.AreEqual(KerberosKdcOptions.None, kdc.Requests.Single().Body.Options & KerberosKdcOptions.Forwarded);
        AssertNoDelegation(acceptor);
    }

    [TestMethod]
    public async Task Delegation_AlwaysWithTicketGrantingTicketNotForwardable_SendsNoDelegation()
    {
        FakeKdc kdc = new();

        FakeGssAcceptor acceptor = await EstablishAsync(
            SecurityDelegation.Always,
            Tickets(kdc, () => Cache(Cached(KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), new KerberosKey(18, [.. FakeKdc.TicketGrantingSessionKey]), KerberosTicketFlags.Initial))));

        Assert.HasCount(1, kdc.Requests);
        AssertNoDelegation(acceptor);
    }

    [TestMethod]
    public async Task Delegation_AlwaysAndKdcRefusesToForward_SendsNoDelegation()
    {
        FakeKdc kdc = new() { Override = request => request.Body.Options.HasFlag(KerberosKdcOptions.Forwarded) ? FakeKdc.Error(13) : null };

        FakeGssAcceptor acceptor = await EstablishAsync(SecurityDelegation.Always, Tickets(kdc, () => Cache(TicketGrantingTicket())));

        Assert.HasCount(2, kdc.Requests);
        AssertNoDelegation(acceptor);
    }

    [TestMethod]
    public async Task Delegation_AlwaysAndCredentialCacheUnreadableTheSecondTime_SendsNoDelegation()
    {
        FakeKdc kdc = new();
        KerberosConfiguration configuration = Configuration(string.Empty);
        int reads = 0;
        KerberosServiceTicketSource tickets = new(
            () => configuration,
            () => ++reads == 1 ? Cache(TicketGrantingTicket()) : throw new KerberosFileException(KerberosFileError.NotFound),
            _ => Client(configuration, kdc));

        AssertNoDelegation(await EstablishAsync(SecurityDelegation.Always, tickets));
    }

    [TestMethod]
    public async Task Delegation_AlwaysAndKrb5ConfUnreadableTheSecondTime_SendsNoDelegation()
    {
        FakeKdc kdc = new();
        KerberosConfiguration configuration = Configuration(string.Empty);
        int reads = 0;
        KerberosServiceTicketSource tickets = new(
            () => ++reads == 1 ? configuration : throw new KerberosConfigurationException(KerberosConfigurationError.SectionSyntax, "bad"),
            () => Cache(TicketGrantingTicket()),
            _ => Client(configuration, kdc));

        AssertNoDelegation(await EstablishAsync(SecurityDelegation.Always, tickets));
    }

    [TestMethod]
    public async Task Delegation_AlwaysWithTicketGrantingTicketOfAnUnknownEncryptionType_SendsNoDelegation()
    {
        FakeKdc kdc = new();

        FakeGssAcceptor acceptor = await EstablishAsync(
            SecurityDelegation.Always,
            Tickets(kdc, () => Cache(ServiceTicket(KerberosTicketFlags.Forwardable), Cached(KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), new KerberosKey(1, new byte[8])))));

        Assert.IsEmpty(kdc.Exchanges);
        AssertNoDelegation(acceptor);
    }

    /// <summary>A cached ticket for the service, in the session key the acceptor holds.</summary>
    private static CachedCredential ServiceTicket(KerberosTicketFlags flags) =>
        Cached(new KerberosPrincipal(KerberosServiceTicketSource.HostBasedServiceNameType, FakeKdc.Realm, ["HTTP", Host]), new KerberosKey(18, [.. FakeKdc.ServiceSessionKey]), flags);

    private static uint ChecksumFlags(FakeGssAcceptor acceptor) => BinaryPrimitives.ReadUInt32LittleEndian(acceptor.Authenticator!.Checksum!.Value.AsSpan(20));

    private static void AssertNoDelegation(FakeGssAcceptor acceptor)
    {
        Assert.AreEqual(0u, ChecksumFlags(acceptor) & DelegationFlag);
        Assert.HasCount(24, acceptor.Authenticator!.Checksum!.Value, "No DlgOpt, Dlgth or KRB-CRED.");
    }

    /// <summary>Runs the whole exchange with <paramref name="delegation" /> asked for, and gives the acceptor that read the initial token.</summary>
    private static async Task<FakeGssAcceptor> EstablishAsync(SecurityDelegation delegation, KerberosServiceTicketSource tickets, SecurityMechanism mechanism = SecurityMechanism.Kerberos)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(tickets).Create(Request(mechanism) with { Delegation = delegation });

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        Assert.AreEqual(SecurityContextStatus.ContinueNeeded, first.Status);
        bool negotiate = mechanism == SecurityMechanism.Negotiate;
        acceptor.Accept(negotiate ? ReadNegTokenInit(first.Token).KerberosToken : first.Token);
        byte[] reply = negotiate
            ? new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode()
            : acceptor.Reply();
        SecurityContextStep second = await context.NextTokenAsync(reply, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        return acceptor;
    }
}
