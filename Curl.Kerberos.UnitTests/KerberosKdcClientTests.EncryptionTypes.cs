namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosKdcClient" /> offers, in its AS-REQs and TGS-REQs, the
/// encryption types <c>default_tkt_enctypes</c> and <c>default_tgs_enctypes</c> resolve to
/// (ADR-0209), keeping only the types it has, and fails before sending when none is left.
/// </summary>
public sealed partial class KerberosKdcClientTests
{
    [TestMethod]
    public void EncryptionTypes_NoLibdefaults_AreMitsDefaultThatThisLibraryHas()
    {
        KerberosKdcClient client = ClientFor(new FakeKdc(), string.Empty);

        CollectionAssert.AreEqual(new[] { 18, 17, 20, 19, 25, 26 }, client.AsRequestEncryptionTypes.ToArray());
        CollectionAssert.AreEqual(new[] { 18, 17, 20, 19, 25, 26 }, client.TgsRequestEncryptionTypes.ToArray());
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_DefaultTicketEncryptionTypesSet_OffersThemResolvedInEveryAsRequest()
    {
        FakeKdc kdc = new();
        KerberosKdcClient client = ClientFor(kdc, " permitted_enctypes = aes128-cts\n default_tkt_enctypes = rc4 aes128-sha2 aes256-cts camellia\n");

        using KerberosCredential credential = await client.GetInitialTicketAsync(AlicePassword, KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), CancellationToken.None);

        Assert.HasCount(2, kdc.Requests);
        Assert.IsTrue(kdc.Requests.All(request => request.Body.EncryptionTypes.SequenceEqual([23, 19, 18, 26, 25])));
        Assert.AreEqual(18, KerberosEncryptedData.Decode(kdc.Requests[1].PreAuthenticationData.Single().Value).EncryptionType);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_DefaultTgsEncryptionTypesSet_OffersThemResolved()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(FakeKdc.TicketGrantingSessionKey, 18);
        FakeKdc kdc = new();
        KerberosKdcClient client = ClientFor(kdc, " default_tgs_enctypes = DEFAULT -aes256-cts +rc4\n");

        using KerberosCredential credential = await client.GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, FakeKdc.Service, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 17, 20, 19, 25, 26, 23 }, kdc.Requests.Single().Body.EncryptionTypes.ToArray());
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_NoTicketEncryptionTypeThisLibraryHas_ThrowsEncryptionTypeNotSupportedWithoutSending()
    {
        FakeKdc kdc = new();

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc, " default_tkt_enctypes = des3\n").GetInitialTicketAsync(AlicePassword, KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.EncryptionTypeNotSupported, failure.Error);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_NoTgsEncryptionTypeThisLibraryHas_ThrowsEncryptionTypeNotSupportedWithoutSending()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(FakeKdc.TicketGrantingSessionKey, 18);
        FakeKdc kdc = new();

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc, " permitted_enctypes = des3 bogus\n").GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, FakeKdc.Service, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.EncryptionTypeNotSupported, failure.Error);
        Assert.IsEmpty(kdc.Exchanges);
    }

    private static KerberosKdcClient ClientFor(FakeKdc kdc, string libdefaults)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse("[libdefaults]\n" + libdefaults + "[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n", "test.conf", root);
        byte[] random = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];
        return new KerberosKdcClient(new KerberosConfiguration(root), new FakeSrvLookup(), kdc, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(random));
    }
}
