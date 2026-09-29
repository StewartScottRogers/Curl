namespace Curl.Kerberos;

/// <summary>
/// In-memory KDCs of several realms that answer TGS-REQs over <see cref="IKerberosKdcTransport" />,
/// each reached at <c>kdc.&lt;realm in lower case&gt;</c>. <see cref="Answer" /> says which server's
/// ticket a realm's KDC issues for a request: the service's own, or a cross-realm
/// <c>krbtgt</c> as a referral. Every ticket it issues gets its own session key, and every
/// request must be authenticated in the key of the ticket-granting ticket it carries, so a
/// client that follows a referral with the wrong ticket fails the test.
/// </summary>
internal sealed class FakeReferralKdcs : IKerberosKdcTransport
{
    private static readonly IKerberosRandomSource Confounders = new FixedKerberosRandomSource(new byte[32]);

    private readonly Dictionary<string, byte[]> sessionKeys = [];

    /// <summary>Gets or sets the server a realm's KDC issues a ticket for, given the realm and the request.</summary>
    public Func<string, KerberosKdcRequestBody, KerberosPrincipal> Answer { get; set; } = (_, body) => new(body.ServerName!.NameType, body.Realm, body.ServerName.Components);

    /// <summary>Gets or sets an error code a realm's KDC answers with instead, or <see langword="null" /> for none.</summary>
    public Func<string, int?> ErrorCode { get; set; } = _ => null;

    public List<string> Exchanges { get; } = [];

    public List<KerberosKdcRequest> Requests { get; } = [];

    /// <summary>Gives the session key of the ticket for <paramref name="server" />, the same every time it is asked.</summary>
    public byte[] SessionKeyFor(KerberosPrincipal server)
    {
        string name = server.ToString();
        if (!sessionKeys.TryGetValue(name, out byte[]? key))
        {
            key = Enumerable.Repeat((byte)(0x30 + sessionKeys.Count), 32).ToArray();
            sessionKeys.Add(name, key);
        }

        return key;
    }

    public static KerberosTicket TicketFor(KerberosPrincipal server) =>
        new(server.Realm, new KerberosPrincipalName(server.NameType, server.Components), new KerberosEncryptedData(18, 1, [0xDE, 0xAD, 0xBE, 0xEF]));

    public Task<byte[]> ExchangeDatagramAsync(string host, int port, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        Exchanges.Add($"udp {host}:{port}");
        string realm = host["kdc.".Length..].ToUpperInvariant();
        return Task.FromResult(AnswerTgs(realm, KerberosKdcRequest.Decode(request)));
    }

    public Task<Stream> ConnectStreamAsync(string host, int port, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Every exchange fits in a datagram.");

    private byte[] AnswerTgs(string realm, KerberosKdcRequest request)
    {
        Requests.Add(request);
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, Confounders);
        KerberosApRequest apRequest = KerberosApRequest.Decode(request.PreAuthenticationData.Single(data => data.DataType == KerberosPreAuthenticationData.TgsRequest).Value);
        KerberosPrincipal granting = new(apRequest.Ticket.ServerName.NameType, apRequest.Ticket.Realm, apRequest.Ticket.ServerName.Components);
        CollectionAssert.AreEqual(new[] { "krbtgt", realm }, granting.Components.ToArray(), "A KDC takes only its own realm's ticket-granting tickets.");
        byte[] grantingKey = SessionKeyFor(granting);
        KerberosAuthenticator authenticator = KerberosAuthenticator.Decode(encryption.Decrypt(grantingKey, 7, apRequest.Authenticator.Cipher));
        if (ErrorCode(realm) is { } code)
        {
            return FakeKdc.Error(code);
        }

        KerberosPrincipal server = Answer(realm, request.Body);
        KerberosEncryptedKdcReplyPart part = new()
        {
            ReplyType = KerberosMessageType.TgsReply,
            Key = new KerberosKey(18, [.. SessionKeyFor(server)]),
            LastRequests = [],
            Nonce = request.Body.Nonce,
            Flags = KerberosTicketFlags.PreAuthenticated,
            AuthenticationTime = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 9, 28, 22, 0, 0, TimeSpan.Zero),
            ServerRealm = server.Realm,
            ServerName = new KerberosPrincipalName(server.NameType, server.Components),
        };
        return new KerberosKdcReply
        {
            MessageType = KerberosMessageType.TgsReply,
            ClientRealm = authenticator.ClientRealm,
            ClientName = authenticator.ClientName,
            Ticket = TicketFor(server),
            EncryptedPart = new KerberosEncryptedData(18, null, encryption.Encrypt(grantingKey, 8, part.Encode())),
        }.Encode();
    }
}
