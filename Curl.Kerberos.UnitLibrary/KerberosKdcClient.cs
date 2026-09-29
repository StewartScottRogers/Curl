using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Gets tickets from the KDC (RFC 4120 sections 3.1 and 3.3, ADR-0168): a service ticket
/// from the credential cache when it holds a live one, otherwise by a TGS exchange with the
/// cache's ticket-granting ticket; or, from a password, a ticket-granting ticket by an AS
/// exchange with <c>PA-ENC-TIMESTAMP</c> pre-authentication and then the service ticket by
/// a TGS exchange, following the KDCs' cross-realm referrals (ADR-0200). Every KRB-ERROR
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

    private readonly KerberosKdcSender sender;
    private readonly TimeProvider timeProvider;
    private readonly IKerberosRandomSource randomSource;

    /// <summary>Initializes a new instance of the <see cref="KerberosKdcClient" /> class.</summary>
    /// <param name="configuration">The parsed <c>krb5.conf</c>: where the KDCs are and how large a UDP request may be.</param>
    /// <param name="srvLookup">Looks up KDC SRV records when <c>krb5.conf</c> names no KDC.</param>
    /// <param name="transport">Reaches the KDCs.</param>
    /// <param name="timeProvider">Gives the time for ticket lifetimes, timestamps and authenticators.</param>
    /// <param name="randomSource">Gives nonces and confounders.</param>
    public KerberosKdcClient(
        KerberosConfiguration configuration,
        IKerberosSrvLookup srvLookup,
        IKerberosKdcTransport transport,
        TimeProvider timeProvider,
        IKerberosRandomSource randomSource)
    {
        sender = new KerberosKdcSender(configuration, new KerberosKdcLocator(configuration, srvLookup), transport);
        this.timeProvider = timeProvider;
        this.randomSource = randomSource;
    }

    /// <summary>
    /// Gets the encryption types every request offers, in order of preference: MIT's default
    /// order of the types this library has (ADR-0168).
    /// </summary>
    public static IReadOnlyList<int> RequestedEncryptionTypes { get; } =
    [
        (int)KerberosEncryptionType.Aes256CtsHmacSha196,
        (int)KerberosEncryptionType.Aes128CtsHmacSha196,
        (int)KerberosEncryptionType.Aes256CtsHmacSha384192,
        (int)KerberosEncryptionType.Aes128CtsHmacSha256128,
        (int)KerberosEncryptionType.Rc4Hmac,
    ];

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
        DateTimeOffset now = timeProvider.GetUtcNow() + (cache.KdcTimeOffset ?? TimeSpan.Zero);
        CachedCredential? serviceTicket = FindLive(cache, server, now);
        if (serviceTicket is not null)
        {
            return KerberosCredential.FromCache(serviceTicket);
        }

        CachedCredential? ticketGrantingTicket = FindLive(cache, TicketGrantingServer(cache.DefaultPrincipal.Realm), now)
            ?? throw new KerberosKdcException(KerberosKdcError.NoCredentials);
        using KerberosCredential granting = KerberosCredential.FromCache(ticketGrantingTicket);
        return await GetTicketFromTicketGrantingServiceAsync(granting, server, cancellationToken).ConfigureAwait(false);
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
    /// cross-realm referrals (RFC 6806 section 8, ADR-0200): a TGS-REP carrying
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
                KerberosCredential reply = await ExchangeWithTicketGrantingServiceAsync(granting, server, cancellationToken).ConfigureAwait(false);
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

    /// <summary>One TGS exchange: asks the KDCs of <paramref name="ticketGrantingTicket" />'s realm for <paramref name="server" /> with <c>canonicalize</c>.</summary>
    private async Task<KerberosCredential> ExchangeWithTicketGrantingServiceAsync(KerberosCredential ticketGrantingTicket, KerberosPrincipal server, CancellationToken cancellationToken)
    {
        KerberosEncryption encryption = EncryptionOf(ticketGrantingTicket.SessionKey.EncryptionType);
        KerberosKdcRequestBody body = new()
        {
            Options = KerberosKdcOptions.Canonicalize,
            Realm = server.Realm,
            ServerName = NameOf(server),
            Till = ticketGrantingTicket.EndTime,
            Nonce = NextNonce(),
            EncryptionTypes = RequestedEncryptionTypes,
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
        return CredentialFrom(kdcReply, plaintext, body.Nonce, expectedServer: null);
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
            return encryption.Decrypt(key, usage, encrypted.Cipher);
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
            EndTime = part.EndTime,
        };
    }

    private KerberosEncryption EncryptionOf(int encryptionType) =>
        RequestedEncryptionTypes.Contains(encryptionType)
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
            EncryptionTypes = RequestedEncryptionTypes,
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
            ? RequestedEncryptionTypes[0]
            : keyInfo.Select(entry => entry.EncryptionType).FirstOrDefault(RequestedEncryptionTypes.Contains);
        KerberosEncryption encryption = EncryptionOf(encryptionType);
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
        KerberosEncryption encryption = EncryptionOf(kdcReply.EncryptedPart.EncryptionType);
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
