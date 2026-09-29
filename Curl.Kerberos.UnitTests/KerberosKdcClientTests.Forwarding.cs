namespace Curl.Kerberos;

// <summary>
// Checks that <see cref="KerberosKdcClient.GetForwardedTicketGrantingTicketAsync" /> turns a
// forwardable ticket-granting ticket into a forwarded one by a TGS-REQ with the
// <c>forwarded</c> option, as MIT's <c>krb5_fwd_tgt_creds</c> does (RFC 4120 sections 2.6
// and 3.3, ADR-0210), and refuses a ticket that is not forwardable before sending anything.
// </summary>
public sealed partial class KerberosKdcClientTests
{
    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_ForwardableTicket_AsksTheTicketsRealmForAForwardedTicket()
    {
        FakeKdc kdc = new();
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicketWith(KerberosTicketFlags.Forwardable | KerberosTicketFlags.Initial);

        using KerberosCredential forwarded = await ClientFor(kdc).GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88" }, kdc.Exchanges);
        KerberosKdcRequest request = kdc.Requests.Single();
        Assert.AreEqual(KerberosMessageType.TgsRequest, request.MessageType);
        Assert.AreEqual(KerberosKdcOptions.Forwarded | KerberosKdcOptions.Forwardable, request.Body.Options);
        Assert.AreEqual(FakeKdc.Realm, request.Body.Realm);
        Assert.AreEqual(KerberosKdcClient.ServiceInstanceNameType, request.Body.ServerName!.NameType);
        CollectionAssert.AreEqual(new[] { "krbtgt", FakeKdc.Realm }, request.Body.ServerName.Components.ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 9, 28, 20, 0, 0, TimeSpan.Zero), request.Body.Till, "The ticket's end, to the second KerberosTime carries.");
        Assert.IsNull(request.Body.RenewTill);
        Assert.IsEmpty(request.Body.Addresses, "MIT asks for an addressless ticket (noaddresses).");
        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", forwarded.Server.ToString());
        Assert.AreEqual("alice@EXAMPLE.TEST", forwarded.Client.ToString());
        Assert.IsTrue(forwarded.Flags.HasFlag(KerberosTicketFlags.Forwarded));
        CollectionAssert.AreEqual(FakeKdc.ServiceSessionKey, forwarded.SessionKey.Value.ToArray());
        CollectionAssert.AreEqual(FakeKdc.TicketGrantingSessionKey, ticketGrantingTicket.SessionKey.Value.ToArray(), "The caller's ticket-granting ticket stays usable.");
    }

    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_RenewableProxiableTicket_CarriesThoseOptionsAndItsRenewUntil()
    {
        FakeKdc kdc = new();
        DateTimeOffset renewUntil = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicketWith(
            KerberosTicketFlags.Forwardable | KerberosTicketFlags.Proxiable | KerberosTicketFlags.Renewable | KerberosTicketFlags.PreAuthenticated,
            renewUntil);

        using KerberosCredential forwarded = await ClientFor(kdc).GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, CancellationToken.None);

        KerberosKdcRequestBody body = kdc.Requests.Single().Body;
        Assert.AreEqual(KerberosKdcOptions.Forwarded | KerberosKdcOptions.Forwardable | KerberosKdcOptions.Proxiable | KerberosKdcOptions.Renewable, body.Options);
        Assert.AreEqual(renewUntil, body.RenewTill);
    }

    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_TicketNotForwardable_ThrowsTicketNotForwardableAndSendsNothing()
    {
        FakeKdc kdc = new();
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicketWith(KerberosTicketFlags.Initial | KerberosTicketFlags.Renewable);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.TicketNotForwardable, failure.Error);
        Assert.IsNull(failure.KdcErrorCode);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_ReplyNamesAnotherServer_ThrowsUnexpectedReply()
    {
        FakeKdc kdc = new() { ReplyServerName = new KerberosPrincipalName(2, ["krbtgt", "OTHER.TEST"]) };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicketWith(KerberosTicketFlags.Forwardable);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.UnexpectedReply, failure.Error);
    }

    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_KdcRefuses_ThrowsItsError()
    {
        FakeKdc kdc = new() { Override = _ => FakeKdc.Error(13) };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicketWith(KerberosTicketFlags.Forwardable);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.KdcRefused, failure.Error);
        Assert.AreEqual(13, failure.KdcErrorCode, "KDC_ERR_BADOPTION.");
    }

    [TestMethod]
    public async Task GetForwardedTicketGrantingTicketAsync_TicketNull_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ClientFor(new FakeKdc()).GetForwardedTicketGrantingTicketAsync(null!, CancellationToken.None));
    }

    private static KerberosCredential TicketGrantingTicketWith(KerberosTicketFlags flags, DateTimeOffset? renewUntil = null) => new()
    {
        Client = FakeKdc.Alice,
        Server = KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm),
        Ticket = FakeKdc.TicketGrantingTicket,
        SessionKey = new KerberosKey(18, [.. FakeKdc.TicketGrantingSessionKey]),
        Flags = flags,
        AuthenticationTime = FakeKdc.Now,
        EndTime = FakeKdc.Now.AddHours(8),
        RenewUntil = renewUntil,
    };
}
