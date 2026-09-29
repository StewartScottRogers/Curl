namespace Curl.Kerberos;

// <summary>
// Checks that <see cref="KerberosKdcClient.GetTicketFromTicketGrantingServiceAsync" /> follows
// the KDCs' cross-realm referrals (RFC 6806 section 8, ADR-0200) through
// <see cref="FakeReferralKdcs" />: a TGS-REP carrying <c>krbtgt/OTHER@REALM</c> is used to ask
// OTHER's KDCs, up to <see cref="KerberosKdcClient.MaximumReferralHops" /> times.
// </summary>
public sealed partial class KerberosKdcClientTests
{
    private const string Example = "EXAMPLE.TEST";
    private const string Other = "OTHER.TEST";
    private const string Third = "THIRD.TEST";

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_KdcRefersToAnotherRealm_GetsTheServiceTicketThere()
    {
        FakeReferralKdcs kdcs = new() { Answer = (realm, body) => realm == Example ? CrossRealm(Other, Example) : Principal(body) };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(kdcs);
        KerberosPrincipal server = new(2, Example, ["HTTP", "web.other.test"]);

        using KerberosCredential credential = await ClientFor(kdcs).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, server, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88", "udp kdc.other.test:88" }, kdcs.Exchanges);
        CollectionAssert.AreEqual(new[] { Example, Other }, kdcs.Requests.Select(request => request.Body.Realm).ToArray());
        Assert.IsTrue(kdcs.Requests.All(request => request.Body.Options == KerberosKdcOptions.Canonicalize));
        Assert.AreEqual("HTTP/web.other.test@OTHER.TEST", credential.Server.ToString());
        Assert.AreEqual("alice@EXAMPLE.TEST", credential.Client.ToString());
        Assert.AreEqual(Other, credential.Ticket.Realm);
        CollectionAssert.AreEqual(kdcs.SessionKeyFor(credential.Server), credential.SessionKey.Value.ToArray());
        CollectionAssert.AreEqual(kdcs.SessionKeyFor(ticketGrantingTicket.Server), ticketGrantingTicket.SessionKey.Value.ToArray(), "The caller's ticket-granting ticket stays usable.");
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ServerInAKnownRealmTwoHopsAway_KeepsAskingForThatRealm()
    {
        FakeReferralKdcs kdcs = new()
        {
            Answer = (realm, body) => realm switch
            {
                Example => CrossRealm(Third, Example),
                Third => CrossRealm(Other, Third),
                _ => Principal(body),
            },
        };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(kdcs);
        KerberosPrincipal server = new(2, Other, ["HTTP", "web.other.test"]);

        using KerberosCredential credential = await ClientFor(kdcs).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, server, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88", "udp kdc.third.test:88", "udp kdc.other.test:88" }, kdcs.Exchanges);
        CollectionAssert.AreEqual(new[] { Other, Other, Other }, kdcs.Requests.Select(request => request.Body.Realm).ToArray());
        Assert.AreEqual("HTTP/web.other.test@OTHER.TEST", credential.Server.ToString());
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ReferralLoop_ThrowsReferralLimitExceededAfterTheLastHop()
    {
        FakeReferralKdcs kdcs = new() { Answer = (realm, _) => CrossRealm(realm == Example ? Other : Example, realm) };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(kdcs);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdcs).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, new KerberosPrincipal(2, Example, ["HTTP", "web.other.test"]), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.ReferralLimitExceeded, failure.Error);
        Assert.HasCount(11, kdcs.Requests, "The first request and ten referrals followed (MIT's KRB5_REFERRAL_MAXHOPS).");
        CollectionAssert.AreEqual(kdcs.SessionKeyFor(ticketGrantingTicket.Server), ticketGrantingTicket.SessionKey.Value.ToArray());
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ReferredRealmRefuses_ThrowsItsError()
    {
        FakeReferralKdcs kdcs = new()
        {
            Answer = (_, _) => CrossRealm(Other, Example),
            ErrorCode = realm => realm == Other ? 7 : null,
        };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(kdcs);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdcs).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, new KerberosPrincipal(2, Example, ["HTTP", "web.other.test"]), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.ServerPrincipalUnknown, failure.Error);
        Assert.HasCount(2, kdcs.Requests);
    }

    [TestMethod]
    [DataRow("HTTP/web.other.test", Other, DisplayName = "the service in another realm than asked")]
    [DataRow("HTTP/elsewhere.example.test", Example, DisplayName = "another service")]
    [DataRow("krbtgt/OTHER.TEST", Third, DisplayName = "a referral from another realm than the KDC's")]
    [DataRow("krbtgt/EXAMPLE.TEST", Example, DisplayName = "a referral to the KDC's own realm")]
    [DataRow("krbtgt/OTHER.TEST/extra", Example, DisplayName = "a krbtgt name of three components")]
    [DataRow("krbtgt", Example, DisplayName = "a krbtgt name of one component")]
    public async Task GetTicketFromTicketGrantingServiceAsync_ReplyIsNeitherTheServiceNorAReferral_ThrowsUnexpectedReply(string name, string realm)
    {
        FakeReferralKdcs kdcs = new() { Answer = (_, _) => new KerberosPrincipal(2, realm, name.Split('/')) };
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(kdcs);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdcs).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, new KerberosPrincipal(2, Example, ["HTTP", "web.other.test"]), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.UnexpectedReply, failure.Error);
        Assert.HasCount(1, kdcs.Requests);
    }

    private static KerberosPrincipal CrossRealm(string to, string from) => new(KerberosKdcClient.ServiceInstanceNameType, from, ["krbtgt", to]);

    private static KerberosPrincipal Principal(KerberosKdcRequestBody body) => new(body.ServerName!.NameType, body.Realm, body.ServerName.Components);

    private static KerberosCredential TicketGrantingTicket(FakeReferralKdcs kdcs)
    {
        KerberosPrincipal server = KerberosKdcClient.TicketGrantingServer(Example);
        return new KerberosCredential
        {
            Client = FakeKdc.Alice,
            Server = server,
            Ticket = FakeReferralKdcs.TicketFor(server),
            SessionKey = new KerberosKey(18, [.. kdcs.SessionKeyFor(server)]),
            Flags = KerberosTicketFlags.Initial,
            AuthenticationTime = FakeKdc.Now,
            EndTime = FakeKdc.Now.AddHours(8),
        };
    }

    private static KerberosKdcClient ClientFor(FakeReferralKdcs kdcs)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(
            "[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n OTHER.TEST = {\n kdc = kdc.other.test\n }\n THIRD.TEST = {\n kdc = kdc.third.test\n }\n",
            "test.conf",
            root);
        byte[] random = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];
        return new KerberosKdcClient(new KerberosConfiguration(root), new FakeSrvLookup(), kdcs, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(random));
    }
}
