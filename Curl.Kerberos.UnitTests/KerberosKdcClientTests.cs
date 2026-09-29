namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosKdcClient" /> gets tickets through <see cref="FakeKdc" />: a
/// service ticket from the credential cache or by a TGS exchange with its ticket-granting
/// ticket (RFC 4120 section 3.3), a ticket-granting ticket from a password by an AS exchange
/// with <c>PA-ENC-TIMESTAMP</c> after <c>KDC_ERR_PREAUTH_REQUIRED</c> (section 3.1), and a
/// typed <see cref="KerberosKdcException" /> for every refusal and every reply that is not
/// the one asked for.
/// </summary>
[TestClass]
public sealed partial class KerberosKdcClientTests
{
    private static readonly KerberosPasswordCredential AlicePassword = new(FakeKdc.Alice, FakeKdc.Password);

    private static readonly KerberosPrincipal Bob = new(1, FakeKdc.Realm, ["bob"]);

    [TestMethod]
    public async Task GetServiceTicketAsync_CacheHoldsALiveServiceTicket_CopiesItWithoutAskingTheKdc()
    {
        FakeKdc kdc = new();
        CredentialCache cache = Cache(
            Cached(Bob, FakeKdc.Service, FakeKdc.Now.AddHours(1)),
            Cached(FakeKdc.Alice, FakeKdc.Service, FakeKdc.Now.AddMinutes(-1)),
            Cached(FakeKdc.Alice, FakeKdc.Service, FakeKdc.Now.AddHours(1), FakeKdc.ServiceSessionKey));

        using KerberosCredential credential = await ClientFor(kdc).GetServiceTicketAsync(FakeKdc.Service, cache, CancellationToken.None);
        cache.Dispose();

        Assert.IsEmpty(kdc.Exchanges);
        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", credential.Server.ToString());
        Assert.AreEqual("alice@EXAMPLE.TEST", credential.Client.ToString());
        CollectionAssert.AreEqual(FakeKdc.ServiceSessionKey, credential.SessionKey.Value.ToArray());
        Assert.AreEqual(FakeKdc.Now.AddHours(1), credential.EndTime);
        Assert.AreEqual(FakeKdc.Now.AddHours(-1), credential.AuthenticationTime);
        Assert.AreEqual(KerberosTicketFlags.Forwardable, credential.Flags);
        Assert.AreEqual(FakeKdc.Realm, credential.Ticket.Realm);
    }

    [TestMethod]
    public async Task GetServiceTicketAsync_CacheHoldsALiveTicketGrantingTicket_GetsTheServiceTicketByATgsExchange()
    {
        FakeKdc kdc = new();
        CredentialCache cache = Cache(
            Cached(FakeKdc.Alice, FakeKdc.Service, FakeKdc.Now.AddMinutes(-1)),
            Cached(FakeKdc.Alice, KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), FakeKdc.Now.AddHours(8), FakeKdc.TicketGrantingSessionKey));

        using KerberosCredential credential = await ClientFor(kdc).GetServiceTicketAsync(FakeKdc.Service, cache, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88" }, kdc.Exchanges);
        KerberosKdcRequest request = kdc.Requests.Single();
        Assert.AreEqual(KerberosMessageType.TgsRequest, request.MessageType);
        Assert.AreEqual(FakeKdc.Realm, request.Body.Realm);
        Assert.IsNull(request.Body.ClientName);
        Assert.AreEqual(FakeKdc.Now.AddHours(8).AddMilliseconds(-500), request.Body.Till, "KerberosTime keeps whole seconds.");
        Assert.AreEqual(0x01020304u, request.Body.Nonce);
        CollectionAssert.AreEqual(new[] { 18, 17, 20, 19, 23 }, request.Body.EncryptionTypes.ToArray());
        Assert.AreEqual(FakeKdc.Now.AddMilliseconds(-500), kdc.LastAuthenticator!.ClientTime);
        Assert.AreEqual(500_000, kdc.LastAuthenticator.ClientMicroseconds);
        Assert.AreEqual(16, kdc.LastAuthenticator.Checksum!.ChecksumType);
        Assert.AreEqual("alice@EXAMPLE.TEST", credential.Client.ToString());
        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", credential.Server.ToString());
        CollectionAssert.AreEqual(FakeKdc.ServiceSessionKey, credential.SessionKey.Value.ToArray());
        Assert.AreEqual(KerberosTicketFlags.Initial | KerberosTicketFlags.PreAuthenticated, credential.Flags);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 28, 22, 0, 0, TimeSpan.Zero), credential.EndTime);
    }

    [TestMethod]
    public async Task GetServiceTicketAsync_KdcClockAheadEndsTheTicket_ThrowsNoCredentials()
    {
        FakeKdc kdc = new();
        CredentialCache cache = Cache(
            TimeSpan.FromMinutes(2),
            Cached(FakeKdc.Alice, KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), FakeKdc.Now.AddMinutes(1), FakeKdc.TicketGrantingSessionKey));

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetServiceTicketAsync(FakeKdc.Service, cache, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.NoCredentials, failure.Error);
        Assert.IsNull(failure.KdcErrorCode);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task GetServiceTicketAsync_CacheHoldsOnlyAnotherRealmsTicketGrantingTicket_ThrowsNoCredentials()
    {
        CredentialCache cache = Cache(
            Cached(FakeKdc.Alice, KerberosKdcClient.TicketGrantingServer("OTHER.TEST"), FakeKdc.Now.AddHours(8), FakeKdc.TicketGrantingSessionKey));

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(new FakeKdc()).GetServiceTicketAsync(FakeKdc.Service, cache, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.NoCredentials, failure.Error);
    }

    [TestMethod]
    public async Task GetServiceTicketAsync_Password_GetsATicketGrantingTicketAfterPreAuthenticationThenTheServiceTicket()
    {
        FakeKdc kdc = new();

        using KerberosCredential credential = await ClientFor(kdc).GetServiceTicketAsync(FakeKdc.Service, AlicePassword, CancellationToken.None);

        Assert.HasCount(3, kdc.Requests);
        Assert.IsEmpty(kdc.Requests[0].PreAuthenticationData);
        Assert.AreEqual(KerberosPreAuthenticationData.EncryptedTimestamp, kdc.Requests[1].PreAuthenticationData.Single().DataType);
        Assert.AreEqual(FakeKdc.Now.AddDays(1).AddMilliseconds(-500), kdc.Requests[1].Body.Till, "KerberosTime keeps whole seconds.");
        Assert.AreEqual(new KerberosEncryptedTimestamp(FakeKdc.Now.AddMilliseconds(-500), 500_000), kdc.LastTimestamp);
        Assert.AreEqual(KerberosMessageType.TgsRequest, kdc.Requests[2].MessageType);
        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", credential.Server.ToString());
        CollectionAssert.AreEqual(FakeKdc.ServiceSessionKey, credential.SessionKey.Value.ToArray());
    }

    [TestMethod]
    public async Task GetServiceTicketAsync_HttpsKdcAndAProxyTransport_GetsTheTicketsThroughTheKdcProxy()
    {
        FakeKdc kdc = new();
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse("[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy.example.test/KdcProxy\n }\n", "test.conf", root);
        byte[] random = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];
        KerberosKdcClient client = new(new KerberosConfiguration(root), new FakeSrvLookup(), kdc, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(random), kdc);

        using KerberosCredential credential = await client.GetServiceTicketAsync(FakeKdc.Service, AlicePassword, CancellationToken.None);

        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", credential.Server.ToString());
        Assert.HasCount(3, kdc.Exchanges);
        Assert.IsTrue(kdc.Exchanges.All(exchange => exchange == "https proxy.example.test:443/KdcProxy"));
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_KdcNeedsNoPreAuthentication_DecryptsTheReplyInTheDefaultSaltsKey()
    {
        FakeKdc kdc = new() { RequirePreAuthentication = false };

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        Assert.HasCount(1, kdc.Requests);
        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", credential.Server.ToString());
        Assert.AreEqual("alice@EXAMPLE.TEST", credential.Client.ToString());
        CollectionAssert.AreEqual(FakeKdc.TicketGrantingSessionKey, credential.SessionKey.Value.ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), credential.AuthenticationTime);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyCarriesKeyInfo_DecryptsInTheKeyItsSaltMakes()
    {
        FakeKdc kdc = new() { RequirePreAuthentication = false, SendsKeyInfoInReply = true, Salt = "EXAMPLE.TESTalice-renamed" };

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        CollectionAssert.AreEqual(FakeKdc.TicketGrantingSessionKey, credential.SessionKey.Value.ToArray());
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_PreAuthenticationUsesTheKdcsSalt_Succeeds()
    {
        FakeKdc kdc = new() { Salt = "EXAMPLE.TESTalice-renamed" };

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        Assert.HasCount(2, kdc.Requests);
        Assert.IsNotNull(kdc.LastTimestamp);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_PreAuthenticationRequiredWithoutErrorData_UsesTheFirstTypeAndDefaultSalt()
    {
        FakeKdc kdc = new();
        kdc.Override = request => request.PreAuthenticationData.Count == 0 ? FakeKdc.Error(KerberosErrorMessage.PreAuthenticationRequired) : null;

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        Assert.AreEqual(18, KerberosEncryptedData.Decode(kdc.Requests[1].PreAuthenticationData.Single().Value).EncryptionType);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt()
    {
        FakeKdc kdc = new() { SendsKeyInfo = false };

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        Assert.IsNotNull(kdc.LastTimestamp);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_KeyInfoListsAnUnsupportedTypeFirst_PreAuthenticatesInTheFirstSupportedOne()
    {
        FakeKdc kdc = new() { ClientKeyType = (int)KerberosEncryptionType.Aes128CtsHmacSha196 };
        kdc.Override = request => request.PreAuthenticationData.Count == 0
            ? PreAuthenticationRequired(new KerberosEncryptionTypeInfo2Entry(16, null, null), new KerberosEncryptionTypeInfo2Entry(17, FakeKdc.DefaultSalt, [0, 0, 0x10, 0]))
            : null;

        using KerberosCredential credential = await GetTicketGrantingTicketAsync(kdc);

        Assert.AreEqual(17, KerberosEncryptedData.Decode(kdc.Requests[1].PreAuthenticationData.Single().Value).EncryptionType);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_KeyInfoListsOnlyUnsupportedTypes_ThrowsEncryptionTypeNotSupported()
    {
        FakeKdc kdc = new() { Override = _ => PreAuthenticationRequired(new KerberosEncryptionTypeInfo2Entry(16, null, null)) };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.EncryptionTypeNotSupported, failure.Error);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_WrongPassword_ThrowsPreAuthenticationFailed()
    {
        FakeKdc kdc = new() { Salt = "EXAMPLE.TESTsomeone-else" };
        kdc.Override = request => request.PreAuthenticationData.Count == 0
            ? PreAuthenticationRequired(new KerberosEncryptionTypeInfo2Entry(18, FakeKdc.DefaultSalt, null))
            : null;

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.PreAuthenticationFailed, failure.Error);
        Assert.AreEqual(24, failure.KdcErrorCode);
        Assert.AreEqual("error 24", failure.KdcErrorText);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_WrongPasswordWithoutPreAuthentication_ThrowsReplyIntegrityCheckFailed()
    {
        FakeKdc kdc = new() { RequirePreAuthentication = false, Salt = "EXAMPLE.TESTsomeone-else" };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.ReplyIntegrityCheckFailed, failure.Error);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_PreAuthenticationRequiredAgain_ThrowsPreAuthenticationRequired()
    {
        FakeKdc kdc = new() { Override = _ => PreAuthenticationRequired(new KerberosEncryptionTypeInfo2Entry(18, null, null)) };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.PreAuthenticationRequired, failure.Error);
        Assert.HasCount(2, kdc.Requests);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ClientUnknown_ThrowsClientPrincipalUnknown()
    {
        FakeKdc kdc = new() { Override = _ => FakeKdc.Error(6) };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.ClientPrincipalUnknown, failure.Error);
        Assert.HasCount(1, kdc.Requests);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyIsNotAMessage_ThrowsUnexpectedReply()
    {
        await AssertUnexpectedReplyAsync(new FakeKdc { Override = _ => [0x04, 0x01, 0x00] });
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ErrorDataIsNotMethodData_ThrowsUnexpectedReply()
    {
        await AssertUnexpectedReplyAsync(new FakeKdc { Override = _ => FakeKdc.Error(KerberosErrorMessage.PreAuthenticationRequired, [0x01]) });
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyIsATgsReply_ThrowsUnexpectedReply()
    {
        FakeKdc kdc = new() { RequirePreAuthentication = false };
        kdc.Override = request =>
        {
            kdc.Override = _ => null;
            KerberosKdcReply reply = KerberosKdcReply.Decode(kdc.Answer(request.Encode()));
            return new KerberosKdcReply
            {
                MessageType = KerberosMessageType.TgsReply,
                ClientRealm = reply.ClientRealm,
                ClientName = reply.ClientName,
                Ticket = reply.Ticket,
                EncryptedPart = reply.EncryptedPart,
            }.Encode();
        };

        await AssertUnexpectedReplyAsync(kdc);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyRepeatsAnotherNonce_ThrowsUnexpectedReply()
    {
        await AssertUnexpectedReplyAsync(new FakeKdc { ReplyNonce = nonce => nonce + 1 });
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyNamesAnotherServer_ThrowsUnexpectedReply()
    {
        await AssertUnexpectedReplyAsync(new FakeKdc { ReplyServerName = new KerberosPrincipalName(2, ["krbtgt", "OTHER.TEST"]) });
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyNamesAnotherServerRealm_ThrowsUnexpectedReply()
    {
        await AssertUnexpectedReplyAsync(new FakeKdc { ReplyServerRealm = "OTHER.TEST" });
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_EncryptedPartIsNotAReplyPart_ThrowsUnexpectedReply()
    {
        byte[] key = FakeKdc.ClientKey(18, FakeKdc.DefaultSalt);
        byte[] cipher = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new FixedKerberosRandomSource(new byte[32])).Encrypt(key, 3, [0x05, 0x00]);
        FakeKdc kdc = new() { Override = request => AsReply(request, new KerberosEncryptedData(18, null, cipher)) };

        await AssertUnexpectedReplyAsync(kdc);
    }

    [TestMethod]
    public async Task GetInitialTicketAsync_ReplyEncryptedInAnUnsupportedType_ThrowsEncryptionTypeNotSupported()
    {
        FakeKdc kdc = new() { Override = request => AsReply(request, new KerberosEncryptedData(16, null, new byte[48])) };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.EncryptionTypeNotSupported, failure.Error);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ServiceUnknown_ThrowsServerPrincipalUnknown()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(FakeKdc.TicketGrantingSessionKey, 18);
        KerberosPrincipal unknown = new(2, FakeKdc.Realm, ["HTTP", "unknown.example.test"]);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(new FakeKdc()).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, unknown, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.ServerPrincipalUnknown, failure.Error);
        Assert.AreEqual(7, failure.KdcErrorCode);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ClockTooFarFromTheKdcs_ThrowsClockSkew()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(FakeKdc.TicketGrantingSessionKey, 18);
        FakeKdc kdc = new() { Override = _ => FakeKdc.Error(37) };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, FakeKdc.Service, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.ClockSkew, failure.Error);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_ReplyNotInTheSessionKey_ThrowsUnexpectedReply()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(FakeKdc.TicketGrantingSessionKey, 18);
        FakeKdc kdc = new() { Override = TgsReplyInWrongKey };

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, FakeKdc.Service, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.UnexpectedReply, failure.Error);
    }

    [TestMethod]
    public async Task GetTicketFromTicketGrantingServiceAsync_SessionKeyOfAnUnsupportedType_ThrowsEncryptionTypeNotSupported()
    {
        using KerberosCredential ticketGrantingTicket = TicketGrantingTicket(new byte[24], 16);
        FakeKdc kdc = new();

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => ClientFor(kdc).GetTicketFromTicketGrantingServiceAsync(ticketGrantingTicket, FakeKdc.Service, CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.EncryptionTypeNotSupported, failure.Error);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public void TicketGrantingServer_Realm_IsKrbtgtOfTheRealm()
    {
        KerberosPrincipal server = KerberosKdcClient.TicketGrantingServer("EXAMPLE.TEST");

        Assert.AreEqual(KerberosKdcClient.ServiceInstanceNameType, server.NameType);
        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", server.ToString());
    }

    private static KerberosKdcClient ClientFor(FakeKdc kdc)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse("[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n", "test.conf", root);
        byte[] random = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];
        return new KerberosKdcClient(new KerberosConfiguration(root), new FakeSrvLookup(), kdc, new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(random));
    }

    private static Task<KerberosCredential> GetTicketGrantingTicketAsync(FakeKdc kdc) =>
        ClientFor(kdc).GetInitialTicketAsync(AlicePassword, KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm), CancellationToken.None);

    private static async Task AssertUnexpectedReplyAsync(FakeKdc kdc)
    {
        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(() => GetTicketGrantingTicketAsync(kdc));

        Assert.AreEqual(KerberosKdcError.UnexpectedReply, failure.Error);
    }

    private static byte[] PreAuthenticationRequired(params KerberosEncryptionTypeInfo2Entry[] entries) =>
        FakeKdc.Error(
            KerberosErrorMessage.PreAuthenticationRequired,
            KerberosPreAuthenticationData.EncodeMethodData([new(KerberosPreAuthenticationData.EncryptionTypeInfo2, KerberosEncryptionTypeInfo2Entry.EncodeList(entries))]));

    private static byte[] AsReply(KerberosKdcRequest request, KerberosEncryptedData encryptedPart) => new KerberosKdcReply
    {
        MessageType = KerberosMessageType.AsReply,
        ClientRealm = FakeKdc.Realm,
        ClientName = request.Body.ClientName!,
        Ticket = FakeKdc.TicketGrantingTicket,
        EncryptedPart = encryptedPart,
    }.Encode();

    private static byte[] TgsReplyInWrongKey(KerberosKdcRequest request) => new KerberosKdcReply
    {
        MessageType = KerberosMessageType.TgsReply,
        ClientRealm = FakeKdc.Realm,
        ClientName = new KerberosPrincipalName(1, ["alice"]),
        Ticket = FakeKdc.TicketFor(request.Body.ServerName!),
        EncryptedPart = new KerberosEncryptedData(18, null, new byte[64]),
    }.Encode();

    private static KerberosCredential TicketGrantingTicket(byte[] sessionKey, int encryptionType) => new()
    {
        Client = FakeKdc.Alice,
        Server = KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm),
        Ticket = FakeKdc.TicketGrantingTicket,
        SessionKey = new KerberosKey(encryptionType, [.. sessionKey]),
        Flags = KerberosTicketFlags.Initial,
        AuthenticationTime = FakeKdc.Now,
        EndTime = FakeKdc.Now.AddHours(8),
    };

    private static CredentialCache Cache(params CachedCredential[] credentials) => Cache(null, credentials);

    private static CredentialCache Cache(TimeSpan? kdcTimeOffset, params CachedCredential[] credentials) =>
        new(kdcTimeOffset, FakeKdc.Alice, credentials);

    private static CachedCredential Cached(KerberosPrincipal client, KerberosPrincipal server, DateTimeOffset endTime, byte[]? sessionKey = null) => new()
    {
        Client = client,
        Server = server,
        SessionKey = new KerberosKey(18, [.. sessionKey ?? new byte[32]]),
        AuthenticationTime = FakeKdc.Now.AddHours(-1),
        StartTime = DateTimeOffset.UnixEpoch,
        EndTime = endTime,
        RenewUntil = DateTimeOffset.UnixEpoch,
        IsEncryptedInSessionKey = false,
        Flags = KerberosTicketFlags.Forwardable,
        Addresses = [],
        AuthorizationData = [],
        Ticket = FakeKdc.TicketFor(new KerberosPrincipalName(server.NameType, server.Components)).Encode(),
        SecondTicket = [],
    };
}
