using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Gets tickets from the KDC (RFC 4120 sections 3.1 and 3.3, ADR-0168): a service ticket
/// from the credential cache when it holds a live one, otherwise by a TGS exchange with the
/// cache's ticket-granting ticket; or, from a password, a ticket-granting ticket by an AS
/// exchange with <c>PA-ENC-TIMESTAMP</c> pre-authentication and then the service ticket by
/// a TGS exchange, following the KDCs' cross-realm referrals (ADR-0256); and, for
/// <c>--delegation</c>, a forwarded ticket-granting ticket from a forwardable one (ADR-0210). Every KRB-ERROR
/// becomes a <see cref="KerberosKdcException" />.
/// </summary>
public sealed class KerberosKdcClient
{
    /// <summary>The <c>KRB_NT_SRV_INST</c> name type of a <c>krbtgt</c> principal.</summary>
    public const int ServiceInstanceNameType = 2;

    /// <summary>
    /// The most cross-realm referrals one TGS request follows before it fails with
    /// <see cref="KerberosKdcError.ReferralLimitExceeded" />: MIT's <c>KRB5_REFERRAL_MAXHOPS</c>.
    /// </summary>
    public const int MaximumReferralHops = 10;

    private const string TicketGrantingServiceName = "krbtgt";
    private const int EncryptedTimestampUsage = 1;
    private const int AsReplyUsage = 3;
    private const int TgsRequestChecksumUsage = 6;
    private const int TgsRequestAuthenticatorUsage = 7;
    private const int TgsReplyUsage = 8;

    private static readonly TimeSpan TicketLifetime = TimeSpan.FromDays(1);

    /// <summary>The encryption types <see cref="KerberosEncryption.Create" /> has, by number.</summary>
    private static readonly int[] ImplementedEncryptionTypes = [.. Enum.GetValues<KerberosEncryptionType>().Select(type => (int)type)];

    private readonly KerberosKdcSender sender;
    private readonly TimeProvider timeProvider;
    private readonly IKerberosRandomSource randomSource;

    /// <summary>Initializes a new instance of the <see cref="KerberosKdcClient" /> class.</summary>
    /// <param name="configuration">The parsed <c>krb5.conf</c>: where the KDCs are and how large a UDP request may be.</param>
    /// <param name="srvLookup">Looks up KDC SRV records when <c>krb5.conf</c> names no KDC.</param>
    /// <param name="transport">Reaches the KDCs.</param>
    /// <param name="timeProvider">Gives the time for ticket lifetimes, timestamps and authenticators.</param>
    /// <param name="randomSource">Gives nonces and confounders.</param>
    /// <param name="proxyTransport">Reaches <c>https://</c> KDCs through their MS-KKDCP proxy, or <see langword="null" /> to skip them.</param>
    public KerberosKdcClient(
        KerberosConfiguration configuration,
        IKerberosSrvLookup srvLookup,
        IKerberosKdcTransport transport,
        TimeProvider timeProvider,
        IKerberosRandomSource randomSource,
        IKerberosKdcProxyTransport? proxyTransport = null)
    {
        sender = new KerberosKdcSender(configuration, new KerberosKdcLocator(configuration, srvLookup), transport, proxyTransport);
        this.timeProvider = timeProvider;
        this.randomSource = randomSource;
        AsRequestEncryptionTypes = Offerable(configuration.DefaultTicketEncryptionTypes, configuration.AllowWeakCrypto);
        TgsRequestEncryptionTypes = Offerable(configuration.DefaultTicketGrantingServiceEncryptionTypes, configuration.AllowWeakCrypto);
    }

    /// <summary>
    /// Gets the encryption types every AS-REQ offers, in order of preference:
    /// <c>default_tkt_enctypes</c> resolved as MIT does (ADR-0209), keeping the types this
    /// library has. Empty when none is left, and then every AS exchange fails with
    /// <see cref="KerberosKdcError.EncryptionTypeNotSupported" /> before anything is sent.
    /// </summary>
    public IReadOnlyList<int> AsRequestEncryptionTypes { get; }

    /// <summary>
    /// Gets the encryption types every TGS-REQ offers, in order of preference:
    /// <c>default_tgs_enctypes</c> resolved as MIT does (ADR-0209), keeping the types this
    /// library has. Empty when none is left, and then every TGS exchange fails with
    /// <see cref="KerberosKdcError.EncryptionTypeNotSupported" /> before anything is sent.
    /// </summary>
    public IReadOnlyList<int> TgsRequestEncryptionTypes { get; }

    /// <summary>Gives the ticket-granting service of <paramref name="realm" />, <c>krbtgt/REALM@REALM</c>.</summary>
    /// <param name="realm">The realm.</param>
    /// <returns>The principal.</returns>
    public static KerberosPrincipal TicketGrantingServer(string realm) =>
        new(ServiceInstanceNameType, realm, [TicketGrantingServiceName, realm]);

    /// <summary>
    /// Gets a ticket for <paramref name="server" /> from <paramref name="cache" />: its own
    /// live ticket for the server when it has one, otherwise one from a TGS exchange with its
    /// live ticket-granting ticket for its default principal's realm. "Live" is judged by the
    /// time plus the cache's KDC time offset.
    /// </summary>
    /// <param name="server">The service, e.g. <c>HTTP/server.example.test@EXAMPLE.TEST</c>.</param>
    /// <param name="cache">The credential cache; it is only read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The service ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">No ticket could be got; <see cref="KerberosKdcException.Error" /> says why.</exception>
    public async Task<KerberosCredential> GetServiceTicketAsync(KerberosPrincipal server, CredentialCache cache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(cache);
        return LiveServiceTicketIn(cache, server)
            ?? await GetTicketWithCachedTicketGrantingTicketAsync(server, cache, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a ticket for <paramref name="server" /> from the credential cache
    /// <paramref name="cacheName" /> names, as the overload taking a <see cref="CredentialCache" />
    /// does, and stores a ticket got by a TGS exchange back in that cache so the next request
    /// finds it there, as MIT's <c>gss_init_sec_context</c> does (ADR-0208). A cache that
    /// cannot be written leaves the ticket unstored and is otherwise ignored, as MIT ignores
    /// <c>krb5_cc_store_cred</c>'s failure.
    /// </summary>
    /// <param name="server">The service, e.g. <c>HTTP/server.example.test@EXAMPLE.TEST</c>.</param>
    /// <param name="store">Reads the cache and stores the new ticket in it.</param>
    /// <param name="cacheName">The cache's name, e.g. <see cref="CredentialCacheStore.DefaultCacheName" />.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The service ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosFileException">The cache cannot be read; <see cref="KerberosFileException.Error" /> says why.</exception>
    /// <exception cref="KerberosKdcException">No ticket could be got; <see cref="KerberosKdcException.Error" /> says why.</exception>
    public async Task<KerberosCredential> GetServiceTicketAsync(KerberosPrincipal server, CredentialCacheStore store, string cacheName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(store);
        using CredentialCache cache = store.Read(cacheName);
        KerberosCredential? cached = LiveServiceTicketIn(cache, server);
        if (cached is not null)
        {
            return cached;
        }

        KerberosCredential credential = await GetTicketWithCachedTicketGrantingTicketAsync(server, cache, cancellationToken).ConfigureAwait(false);
        try
        {
            store.Store(cacheName, credential);
        }
        catch (KerberosFileException)
        {
            // MIT's tkt_creds_get stores with (void) krb5_cc_store_cred: the ticket is good even when the cache is not.
        }

        return credential;
    }

    /// <summary>Gets a ticket for <paramref name="server" /> from a password: an AS exchange for a ticket-granting ticket, then a TGS exchange.</summary>
    /// <param name="server">The service.</param>
    /// <param name="password">The client and its password.</param>
    /// <param name="cancellationToken">Cancels the exchanges.</param>
    /// <returns>The service ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">No ticket could be got; <see cref="KerberosKdcException.Error" /> says why.</exception>
    public async Task<KerberosCredential> GetServiceTicketAsync(KerberosPrincipal server, KerberosPasswordCredential password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(password);
        using KerberosCredential granting = await GetInitialTicketAsync(password, TicketGrantingServer(password.Client.Realm), cancellationToken).ConfigureAwait(false);
        return await GetTicketFromTicketGrantingServiceAsync(granting, server, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a ticket for <paramref name="server" />, normally the realm's ticket-granting
    /// service, by an AS exchange (RFC 4120 section 3.1). The first AS-REQ goes without
    /// pre-authentication; a <c>KDC_ERR_PREAUTH_REQUIRED</c> answer is followed by a second
    /// with <c>PA-ENC-TIMESTAMP</c> in the key the password makes with the KDC's
    /// <c>PA-ETYPE-INFO2</c> salt.
    /// </summary>
    /// <param name="password">The client and its password.</param>
    /// <param name="server">The server the ticket is for, in the client's realm.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">No ticket could be got; <see cref="KerberosKdcException.Error" /> says why.</exception>
    /// <exception cref="KerberosCryptographyException">The KDC's string-to-key parameters are ones the encryption type refuses.</exception>
    public async Task<KerberosCredential> GetInitialTicketAsync(KerberosPasswordCredential password, KerberosPrincipal server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(server);
        KerberosKdcRequest request = AsRequest(password.Client, server, []);
        byte[] reply = await sender.SendAsync(password.Client.Realm, request.Encode(), cancellationToken).ConfigureAwait(false);
        KerberosErrorMessage? error = ErrorIn(reply);
        if (error is null)
        {
            return ReadAsReply(reply, request.Body.Nonce, password, server, []);
        }

        if (error.ErrorCode != KerberosErrorMessage.PreAuthenticationRequired)
        {
            throw KerberosKdcException.FromErrorMessage(error);
        }

        IReadOnlyList<KerberosEncryptionTypeInfo2Entry> keyInfo = KeyInfoIn(MethodDataIn(error));
        request = AsRequest(password.Client, server, [EncryptedTimestamp(password, keyInfo)]);
        reply = await sender.SendAsync(password.Client.Realm, request.Encode(), cancellationToken).ConfigureAwait(false);
        return ReadAsReply(reply, request.Body.Nonce, password, server, keyInfo);
    }

    /// <summary>
    /// Gets a ticket for <paramref name="server" /> by TGS exchanges (RFC 4120 section 3.3)
    /// with <paramref name="ticketGrantingTicket" />, sent to the KDCs of the realm the
    /// ticket-granting ticket is for, asking with <c>canonicalize</c> and following the KDCs'
    /// cross-realm referrals (RFC 6806 section 8, ADR-0256): a TGS-REP carrying
    /// <c>krbtgt/OTHER@REALM</c> in place of the service's ticket is used to ask OTHER's KDCs,
    /// up to <see cref="MaximumReferralHops" /> times, as MIT's <c>krb5_get_credentials</c> does.
    /// </summary>
    /// <param name="ticketGrantingTicket">A ticket for <c>krbtgt/REALM</c>; it stays the caller's.</param>
    /// <param name="server">The service; a referral from the KDC of its realm moves it to the realm referred to.</param>
    /// <param name="cancellationToken">Cancels the exchanges.</param>
    /// <returns>The service ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">No ticket could be got; <see cref="KerberosKdcException.Error" /> says why.</exception>
    public async Task<KerberosCredential> GetTicketFromTicketGrantingServiceAsync(KerberosCredential ticketGrantingTicket, KerberosPrincipal server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketGrantingTicket);
        ArgumentNullException.ThrowIfNull(server);
        KerberosCredential granting = ticketGrantingTicket;
        try
        {
            for (int referrals = 0; ; referrals++)
            {
                string kdcRealm = granting.Server.Components[^1];
                KerberosCredential reply = await ExchangeWithTicketGrantingServiceAsync(granting, server, KerberosKdcOptions.Canonicalize, expectedServer: null, cancellationToken).ConfigureAwait(false);
                if (SamePrincipal(reply.Server, server))
                {
                    return reply;
                }

                string referredRealm = ReferredRealm(reply, kdcRealm, referrals);
                ReleaseReferral(granting, ticketGrantingTicket);
                granting = reply;
                server = server.Realm == kdcRealm ? new KerberosPrincipal(server.NameType, referredRealm, server.Components) : server;
            }
        }
        finally
        {
            ReleaseReferral(granting, ticketGrantingTicket);
        }
    }

    /// <summary>
    /// Gets a forwarded ticket-granting ticket from <paramref name="cache" />'s live
    /// ticket-granting ticket for its default principal's realm, as the overload taking a
    /// <see cref="KerberosCredential" /> does. "Live" is judged by the time plus the cache's
    /// KDC time offset.
    /// </summary>
    /// <param name="cache">The credential cache; it is only read.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The forwarded ticket-granting ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">
    /// No ticket could be got; <see cref="KerberosKdcException.Error" /> says why:
    /// <see cref="KerberosKdcError.NoCredentials" /> for a cache without a live ticket-granting
    /// ticket, <see cref="KerberosKdcError.TicketNotForwardable" /> for one without <c>forwardable</c>.
    /// </exception>
    public async Task<KerberosCredential> GetForwardedTicketGrantingTicketAsync(CredentialCache cache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        using KerberosCredential granting = CachedTicketGrantingTicket(cache);
        return await GetForwardedTicketGrantingTicketAsync(granting, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a forwarded ticket-granting ticket from <paramref name="ticketGrantingTicket" />
    /// for <c>--delegation</c>, as MIT's <c>krb5_fwd_tgt_creds</c> does for
    /// <c>gss_init_sec_context</c> (ADR-0210): one TGS exchange with the KDCs of the ticket's
    /// realm for <c>krbtgt/REALM@REALM</c>, asking with <c>forwarded</c> and <c>forwardable</c>
    /// plus the ticket's own <c>proxiable</c> and <c>renewable</c>, and for no addresses. A
    /// ticket without <c>forwardable</c> is refused before anything is sent.
    /// </summary>
    /// <param name="ticketGrantingTicket">A ticket for <c>krbtgt/REALM@REALM</c>; it stays the caller's.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The forwarded ticket-granting ticket, for <see cref="KerberosGssContextOptions.ForwardedTicketGrantingTicket" />; the caller disposes it.</returns>
    /// <exception cref="KerberosKdcException">
    /// No ticket could be got; <see cref="KerberosKdcException.Error" /> says why, and is
    /// <see cref="KerberosKdcError.TicketNotForwardable" /> for a ticket without <c>forwardable</c>.
    /// </exception>
    public async Task<KerberosCredential> GetForwardedTicketGrantingTicketAsync(KerberosCredential ticketGrantingTicket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketGrantingTicket);
        if (!ticketGrantingTicket.Flags.HasFlag(KerberosTicketFlags.Forwardable))
        {
            throw new KerberosKdcException(KerberosKdcError.TicketNotForwardable);
        }

        KerberosPrincipal server = TicketGrantingServer(ticketGrantingTicket.Server.Realm);
        KerberosKdcOptions options = ForwardingOptions(ticketGrantingTicket.Flags);
        return await ExchangeWithTicketGrantingServiceAsync(ticketGrantingTicket, server, options, expectedServer: server, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The options a forwarding TGS-REQ asks with: MIT's <c>flags2options</c> of the ticket's
    /// flags (<c>forwardable</c>, <c>proxiable</c>, <c>renewable</c>) with <c>forwarded</c>
    /// added, <c>forwardable</c> kept because <c>gss_init_sec_context</c> asks for it.
    /// </summary>
    private static KerberosKdcOptions ForwardingOptions(KerberosTicketFlags flags)
    {
        const KerberosTicketFlags Carried = KerberosTicketFlags.Forwardable | KerberosTicketFlags.Proxiable | KerberosTicketFlags.Renewable;
        return (KerberosKdcOptions)(uint)(flags & Carried) | KerberosKdcOptions.Forwarded;
    }

    /// <summary>
    /// Gets the realm a TGS-REP from <paramref name="kdcRealm" />'s KDC refers the client to:
    /// OTHER when it carries <c>krbtgt/OTHER@</c><paramref name="kdcRealm" />. Any other server
    /// is <see cref="KerberosKdcError.UnexpectedReply" />, and a referral past the
    /// <see cref="MaximumReferralHops" />th is <see cref="KerberosKdcError.ReferralLimitExceeded" />;
    /// either way <paramref name="reply" /> is disposed.
    /// </summary>
    private static string ReferredRealm(KerberosCredential reply, string kdcRealm, int referralsFollowed)
    {
        IReadOnlyList<string> components = reply.Server.Components;
        bool isReferral = IsReferralFrom(reply.Server, kdcRealm);
        KerberosKdcError? refusal = !isReferral ? KerberosKdcError.UnexpectedReply
            : referralsFollowed >= MaximumReferralHops ? KerberosKdcError.ReferralLimitExceeded
            : null;
        if (refusal is { } error)
        {
            reply.Dispose();
            throw new KerberosKdcException(error);
        }

        return components[1];
    }

    /// <summary>Whether <paramref name="server" /> is <c>krbtgt/OTHER@</c><paramref name="kdcRealm" /> for a realm OTHER other than <paramref name="kdcRealm" />.</summary>
    private static bool IsReferralFrom(KerberosPrincipal server, string kdcRealm) =>
        server.Realm == kdcRealm && server.Components is [TicketGrantingServiceName, string referred] && referred != kdcRealm;

    /// <summary>Disposes a cross-realm ticket-granting ticket got by a referral, never the caller's own.</summary>
    private static void ReleaseReferral(KerberosCredential granting, KerberosCredential callersTicket)
    {
        if (!ReferenceEquals(granting, callersTicket))
        {
            granting.Dispose();
        }
    }

    /// <summary>
    /// One TGS exchange: asks the KDCs of <paramref name="ticketGrantingTicket" />'s realm for
    /// <paramref name="server" /> with <paramref name="options" />, until the ticket's end and,
    /// with <c>renewable</c>, its renew-until time. The reply must name
    /// <paramref name="expectedServer" /> when one is given.
    /// </summary>
    private async Task<KerberosCredential> ExchangeWithTicketGrantingServiceAsync(
        KerberosCredential ticketGrantingTicket,
        KerberosPrincipal server,
        KerberosKdcOptions options,
        KerberosPrincipal? expectedServer,
        CancellationToken cancellationToken)
    {
        KerberosEncryption encryption = EncryptionOf(ticketGrantingTicket.SessionKey.EncryptionType, ImplementedEncryptionTypes);
        KerberosKdcRequestBody body = new()
        {
            Options = options,
            Realm = server.Realm,
            ServerName = NameOf(server),
            Till = ticketGrantingTicket.EndTime,
            RenewTill = options.HasFlag(KerberosKdcOptions.Renewable) ? ticketGrantingTicket.RenewUntil : null,
            Nonce = NextNonce(),
            EncryptionTypes = Offered(TgsRequestEncryptionTypes),
        };
        KerberosKdcRequest request = new()
        {
            MessageType = KerberosMessageType.TgsRequest,
            PreAuthenticationData = [TgsPreAuthentication(ticketGrantingTicket, body, encryption)],
            Body = body,
        };
        byte[] reply = await sender.SendAsync(ticketGrantingTicket.Server.Components[^1], request.Encode(), cancellationToken).ConfigureAwait(false);
        KerberosKdcReply kdcReply = ReadReply(reply, KerberosMessageType.TgsReply);
        byte[] plaintext = Decrypt(encryption, ticketGrantingTicket.SessionKey.Value, TgsReplyUsage, kdcReply.EncryptedPart, KerberosKdcError.UnexpectedReply);
        return CredentialFrom(kdcReply, plaintext, body.Nonce, expectedServer);
    }

    /// <summary>The time the cache's tickets are judged live at: now plus the cache's KDC time offset.</summary>
    private DateTimeOffset NowAtKdc(CredentialCache cache) => timeProvider.GetUtcNow() + (cache.KdcTimeOffset ?? TimeSpan.Zero);

    /// <summary>Copies the cache's own live ticket for <paramref name="server" /> out of it; <see langword="null" /> when it has none.</summary>
    private KerberosCredential? LiveServiceTicketIn(CredentialCache cache, KerberosPrincipal server)
    {
        CachedCredential? serviceTicket = FindLive(cache, server, NowAtKdc(cache));
        return serviceTicket is null ? null : KerberosCredential.FromCache(serviceTicket);
    }

    /// <summary>Gets a ticket for <paramref name="server" /> by a TGS exchange with the cache's live ticket-granting ticket for its default principal's realm.</summary>
    private async Task<KerberosCredential> GetTicketWithCachedTicketGrantingTicketAsync(KerberosPrincipal server, CredentialCache cache, CancellationToken cancellationToken)
    {
        using KerberosCredential granting = CachedTicketGrantingTicket(cache);
        return await GetTicketFromTicketGrantingServiceAsync(granting, server, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Copies the cache's live ticket-granting ticket for its default principal's realm out of it, or fails with <see cref="KerberosKdcError.NoCredentials" />.</summary>
    private KerberosCredential CachedTicketGrantingTicket(CredentialCache cache)
    {
        CachedCredential ticketGrantingTicket = FindLive(cache, TicketGrantingServer(cache.DefaultPrincipal.Realm), NowAtKdc(cache))
            ?? throw new KerberosKdcException(KerberosKdcError.NoCredentials);
        return KerberosCredential.FromCache(ticketGrantingTicket);
    }

    private static CachedCredential? FindLive(CredentialCache cache, KerberosPrincipal server, DateTimeOffset now) =>
        cache.Credentials.FirstOrDefault(credential =>
            SamePrincipal(credential.Client, cache.DefaultPrincipal) && SamePrincipal(credential.Server, server) && credential.EndTime > now);

    private static bool SamePrincipal(KerberosPrincipal first, KerberosPrincipal second) =>
        first.Realm == second.Realm && first.Components.SequenceEqual(second.Components);

    private static KerberosPrincipalName NameOf(KerberosPrincipal principal) => new(principal.NameType, principal.Components);

    private static KerberosKdcException UnexpectedReply() => new(KerberosKdcError.UnexpectedReply);

    /// <summary>Runs a decoder, turning a message that does not decode into <see cref="KerberosKdcError.UnexpectedReply" />.</summary>
    private static T Decode<T>(Func<T> decode)
    {
        try
        {
            return decode();
        }
        catch (KerberosMessageException)
        {
            throw UnexpectedReply();
        }
    }

    private static KerberosErrorMessage? ErrorIn(byte[] reply) =>
        Decode(() => KerberosMessage.PeekType(reply) == KerberosMessageType.Error ? KerberosErrorMessage.Decode(reply) : null);

    private static IReadOnlyList<KerberosPreAuthenticationData> MethodDataIn(KerberosErrorMessage error) =>
        error.ErrorData is null ? [] : Decode(() => KerberosPreAuthenticationData.DecodeMethodData(error.ErrorData));

    private static IReadOnlyList<KerberosEncryptionTypeInfo2Entry> KeyInfoIn(IReadOnlyList<KerberosPreAuthenticationData> data) =>
        Decode(() => data
            .Where(element => element.DataType == KerberosPreAuthenticationData.EncryptionTypeInfo2)
            .Select(element => KerberosEncryptionTypeInfo2Entry.DecodeList(element.Value))
            .FirstOrDefault() ?? []);

    private static KerberosKdcReply ReadReply(byte[] reply, KerberosMessageType expected)
    {
        KerberosErrorMessage? error = ErrorIn(reply);
        if (error is not null)
        {
            throw KerberosKdcException.FromErrorMessage(error);
        }

        KerberosKdcReply kdcReply = Decode(() => KerberosKdcReply.Decode(reply));
        return kdcReply.MessageType == expected ? kdcReply : throw UnexpectedReply();
    }

    private static byte[] Decrypt(KerberosEncryption encryption, ReadOnlySpan<byte> key, int usage, KerberosEncryptedData encrypted, KerberosKdcError failure)
    {
        try
        {
            return KerberosAsn1.WithoutPadding(encryption.Decrypt(key, usage, encrypted.Cipher));
        }
        catch (KerberosCryptographyException)
        {
            throw new KerberosKdcException(failure);
        }
    }

    /// <summary>
    /// Reads the decrypted part and checks it answers the request: the same <paramref name="nonce" />
    /// and, when given, the <paramref name="expectedServer" /> asked for (a TGS-REP may instead
    /// carry a referral, which the caller judges).
    /// </summary>
    private static KerberosCredential CredentialFrom(KerberosKdcReply reply, byte[] plaintext, uint nonce, KerberosPrincipal? expectedServer)
    {
        KerberosEncryptedKdcReplyPart part;
        try
        {
            part = Decode(() => KerberosEncryptedKdcReplyPart.Decode(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        KerberosPrincipal server = new(part.ServerName.NameType, part.ServerRealm, part.ServerName.Components);
        if (part.Nonce != nonce || (expectedServer is not null && !SamePrincipal(server, expectedServer)))
        {
            part.Dispose();
            throw UnexpectedReply();
        }

        return new KerberosCredential
        {
            Client = new KerberosPrincipal(reply.ClientName.NameType, reply.ClientRealm, reply.ClientName.Components),
            Server = server,
            Ticket = reply.Ticket,
            SessionKey = part.Key,
            Flags = part.Flags,
            AuthenticationTime = part.AuthenticationTime,
            StartTime = part.StartTime,
            EndTime = part.EndTime,
            RenewUntil = part.RenewUntil,
            Addresses = part.ClientAddresses,
        };
    }

    /// <summary>Keeps the types of <paramref name="names" />, resolved as MIT does, that this library has.</summary>
    private static int[] Offerable(IReadOnlyList<string> names, bool allowWeakCrypto) =>
        [.. KerberosEncryptionTypeList.Resolve(names, allowWeakCrypto).Where(ImplementedEncryptionTypes.Contains)];

    /// <summary>Gives <paramref name="types" /> to offer, or fails as MIT does when the configuration leaves none.</summary>
    private static IReadOnlyList<int> Offered(IReadOnlyList<int> types) =>
        types.Count > 0 ? types : throw new KerberosKdcException(KerberosKdcError.EncryptionTypeNotSupported);

    private KerberosEncryption EncryptionOf(int encryptionType, IReadOnlyList<int> acceptable) =>
        acceptable.Contains(encryptionType)
            ? KerberosEncryption.Create((KerberosEncryptionType)encryptionType, randomSource)
            : throw new KerberosKdcException(KerberosKdcError.EncryptionTypeNotSupported);

    private KerberosKdcRequest AsRequest(KerberosPrincipal client, KerberosPrincipal server, IReadOnlyList<KerberosPreAuthenticationData> preAuthentication) => new()
    {
        MessageType = KerberosMessageType.AsRequest,
        PreAuthenticationData = preAuthentication,
        Body = new KerberosKdcRequestBody
        {
            Options = KerberosKdcOptions.None,
            ClientName = NameOf(client),
            Realm = client.Realm,
            ServerName = NameOf(server),
            Till = timeProvider.GetUtcNow() + TicketLifetime,
            Nonce = NextNonce(),
            EncryptionTypes = Offered(AsRequestEncryptionTypes),
        },
    };

    /// <summary>A nonce of 31 bits, as MIT sends, since some KDCs read the field as signed.</summary>
    private uint NextNonce()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        randomSource.Fill(bytes);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes) & int.MaxValue;
    }

    /// <summary>The time to the second and the microseconds past it, as timestamps and authenticators carry them.</summary>
    private (DateTimeOffset Time, int Microseconds) Now() => KerberosClock.Now(timeProvider);

    /// <summary>
    /// Makes the client's key for <paramref name="encryption" /> from the password, with the
    /// salt and parameters of the matching <c>PA-ETYPE-INFO2</c> entry, or the default salt
    /// (the realm followed by the name's components) when none matches.
    /// </summary>
    private byte[] ClientKey(KerberosPasswordCredential password, KerberosEncryption encryption, IReadOnlyList<KerberosEncryptionTypeInfo2Entry> keyInfo)
    {
        KerberosEncryptionTypeInfo2Entry? entry = keyInfo.FirstOrDefault(candidate => candidate.EncryptionType == (int)encryption.EncryptionType);
        string salt = entry?.Salt ?? password.Client.Realm + string.Concat(password.Client.Components);
        return encryption.StringToKey(password.Password, Encoding.UTF8.GetBytes(salt), entry?.StringToKeyParameters ?? []);
    }

    /// <summary>
    /// Builds <c>PA-ENC-TIMESTAMP</c> in the first encryption type of the KDC's
    /// <c>PA-ETYPE-INFO2</c> this library has, or the first it asks for when the KDC sent none.
    /// </summary>
    private KerberosPreAuthenticationData EncryptedTimestamp(KerberosPasswordCredential password, IReadOnlyList<KerberosEncryptionTypeInfo2Entry> keyInfo)
    {
        int encryptionType = keyInfo.Count == 0
            ? AsRequestEncryptionTypes[0]
            : keyInfo.Select(entry => entry.EncryptionType).FirstOrDefault(AsRequestEncryptionTypes.Contains);
        KerberosEncryption encryption = EncryptionOf(encryptionType, AsRequestEncryptionTypes);
        byte[] key = ClientKey(password, encryption, keyInfo);
        try
        {
            (DateTimeOffset time, int microseconds) = Now();
            byte[] cipher = encryption.Encrypt(key, EncryptedTimestampUsage, new KerberosEncryptedTimestamp(time, microseconds).Encode());
            return new KerberosPreAuthenticationData(
                KerberosPreAuthenticationData.EncryptedTimestamp,
                new KerberosEncryptedData(encryptionType, null, cipher).Encode());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Decrypts an AS-REP in the key the password makes, salted as the reply's or the error's <c>PA-ETYPE-INFO2</c> says.</summary>
    private KerberosCredential ReadAsReply(byte[] reply, uint nonce, KerberosPasswordCredential password, KerberosPrincipal server, IReadOnlyList<KerberosEncryptionTypeInfo2Entry> errorKeyInfo)
    {
        KerberosKdcReply kdcReply = ReadReply(reply, KerberosMessageType.AsReply);
        KerberosEncryption encryption = EncryptionOf(kdcReply.EncryptedPart.EncryptionType, AsRequestEncryptionTypes);
        byte[] key = ClientKey(password, encryption, [.. KeyInfoIn(kdcReply.PreAuthenticationData), .. errorKeyInfo]);
        byte[] plaintext;
        try
        {
            plaintext = Decrypt(encryption, key, AsReplyUsage, kdcReply.EncryptedPart, KerberosKdcError.ReplyIntegrityCheckFailed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return CredentialFrom(kdcReply, plaintext, nonce, server);
    }

    /// <summary>Builds <c>PA-TGS-REQ</c>: an AP-REQ with the ticket-granting ticket and an authenticator checksumming <paramref name="body" />.</summary>
    private KerberosPreAuthenticationData TgsPreAuthentication(KerberosCredential ticketGrantingTicket, KerberosKdcRequestBody body, KerberosEncryption encryption)
    {
        byte[] checksum = encryption.ComputeChecksum(ticketGrantingTicket.SessionKey.Value, TgsRequestChecksumUsage, body.Encode());
        (DateTimeOffset time, int microseconds) = Now();
        KerberosAuthenticator authenticator = new()
        {
            ClientRealm = ticketGrantingTicket.Client.Realm,
            ClientName = NameOf(ticketGrantingTicket.Client),
            Checksum = new KerberosChecksum(encryption.ChecksumType, checksum),
            ClientMicroseconds = microseconds,
            ClientTime = time,
        };
        byte[] cipher = encryption.Encrypt(ticketGrantingTicket.SessionKey.Value, TgsRequestAuthenticatorUsage, authenticator.Encode());
        KerberosApRequest apRequest = new(
            KerberosApOptions.None,
            ticketGrantingTicket.Ticket,
            new KerberosEncryptedData((int)encryption.EncryptionType, null, cipher));
        return new KerberosPreAuthenticationData(KerberosPreAuthenticationData.TgsRequest, apRequest.Encode());
    }
}
