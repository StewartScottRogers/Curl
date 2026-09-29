using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// An in-memory KDC for realm <see cref="Realm" /> that answers AS-REQs and TGS-REQs over
/// <see cref="IKerberosKdcTransport" />, or through an MS-KKDCP proxy over
/// <see cref="IKerberosKdcProxyTransport" />, with fixed keys, built from this library's own
/// messages and encryption types. It knows one client, <see cref="Alice" /> with password
/// <see cref="Password" />, and one service, <see cref="Service" />. Its properties bend its
/// answers for the failure tests.
/// </summary>
internal sealed class FakeKdc : IKerberosKdcTransport, IKerberosKdcProxyTransport
{
    public const string Realm = "EXAMPLE.TEST";

    public const string Password = "secret";

    public const string DefaultSalt = "EXAMPLE.TESTalice";

    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, 500, TimeSpan.Zero);

    public static readonly KerberosPrincipal Alice = new(1, Realm, ["alice"]);

    public static readonly KerberosPrincipal Service = new(2, Realm, ["HTTP", "server.example.test"]);

    public static readonly KerberosTicket TicketGrantingTicket = TicketFor(new KerberosPrincipalName(2, ["krbtgt", Realm]));

    public static readonly byte[] TicketGrantingSessionKey = Enumerable.Repeat((byte)0x11, 32).ToArray();

    public static readonly byte[] ServiceSessionKey = Enumerable.Repeat((byte)0x22, 32).ToArray();

    private static readonly IKerberosRandomSource Confounders = new FixedKerberosRandomSource(new byte[32]);

    public List<string> Exchanges { get; } = [];

    public List<KerberosKdcRequest> Requests { get; } = [];

    public List<FakeKdcStream> Streams { get; } = [];

    public HashSet<string> UnreachableHosts { get; } = [];

    public bool RequirePreAuthentication { get; set; } = true;

    /// <summary>Gets or sets the encryption type of Alice's key, which the AS-REP is encrypted in.</summary>
    public int ClientKeyType { get; set; } = (int)KerberosEncryptionType.Aes256CtsHmacSha196;

    public string Salt { get; set; } = DefaultSalt;

    /// <summary>Gets or sets whether the KRB-ERROR asking for pre-authentication carries <c>PA-ETYPE-INFO2</c>.</summary>
    public bool SendsKeyInfo { get; set; } = true;

    /// <summary>Gets or sets whether the AS-REP carries <c>PA-ETYPE-INFO2</c>.</summary>
    public bool SendsKeyInfoInReply { get; set; }

    public bool AnswersUdpTooBig { get; set; }

    public uint? TcpReplyLengthPrefix { get; set; }

    public Func<uint, uint> ReplyNonce { get; set; } = nonce => nonce;

    public string? ReplyServerRealm { get; set; }

    public KerberosPrincipalName? ReplyServerName { get; set; }

    /// <summary>Gets or sets the start time the reply's encrypted part gives; <see langword="null" /> leaves it out.</summary>
    public DateTimeOffset? ReplyStartTime { get; set; }

    /// <summary>Gets or sets the renew-until time the reply's encrypted part gives; <see langword="null" /> leaves it out.</summary>
    public DateTimeOffset? ReplyRenewUntil { get; set; }

    /// <summary>Gets or sets the client addresses the reply's encrypted part gives.</summary>
    public IReadOnlyList<KerberosAddress> ReplyAddresses { get; set; } = [];

    /// <summary>Gets or sets an answer given instead of the KDC's own when it returns bytes.</summary>
    public Func<KerberosKdcRequest, byte[]?> Override { get; set; } = _ => null;

    /// <summary>Gets the <c>KDC-PROXY-MESSAGE</c> bodies posted to the proxy.</summary>
    public List<byte[]> ProxyBodies { get; } = [];

    /// <summary>Gets or sets a body the proxy answers with instead of the wrapped answer.</summary>
    public byte[]? ProxyReply { get; set; }

    public KerberosAuthenticator? LastAuthenticator { get; private set; }

    public KerberosEncryptedTimestamp? LastTimestamp { get; private set; }

    public static KerberosTicket TicketFor(KerberosPrincipalName server) =>
        new(Realm, server, new KerberosEncryptedData(18, 1, [0xDE, 0xAD, 0xBE, 0xEF]));

    public static byte[] Error(int code, byte[]? data = null) => new KerberosErrorMessage
    {
        ServerTime = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
        ServerMicroseconds = 0,
        ErrorCode = code,
        Realm = Realm,
        ServerName = new KerberosPrincipalName(2, ["krbtgt", Realm]),
        ErrorText = $"error {code}",
        ErrorData = data,
    }.Encode();

    public static byte[] ClientKey(int encryptionType, string salt) =>
        KerberosEncryption.Create((KerberosEncryptionType)encryptionType, Confounders).StringToKey(Password, Encoding.UTF8.GetBytes(salt), []);

    public Task<byte[]> ExchangeDatagramAsync(string host, int port, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        Exchanges.Add($"udp {host}:{port}");
        ThrowWhenUnreachable(host);
        return Task.FromResult(AnswersUdpTooBig ? Error(KerberosErrorMessage.ResponseTooBig) : Answer(request.ToArray()));
    }

    public Task<Stream> ConnectStreamAsync(string host, int port, CancellationToken cancellationToken)
    {
        Exchanges.Add($"tcp {host}:{port}");
        ThrowWhenUnreachable(host);
        FakeKdcStream stream = new(Answer, TcpReplyLengthPrefix);
        Streams.Add(stream);
        return Task.FromResult<Stream>(stream);
    }

    /// <summary>Plays an MS-KKDCP proxy in front of this KDC: unwraps the request and wraps the answer, or answers <see cref="ProxyReply" />.</summary>
    public Task<byte[]> PostAsync(string host, int port, string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        Exchanges.Add($"https {host}:{port}/{path}");
        ThrowWhenUnreachable(host);
        ProxyBodies.Add(body.ToArray());
        KerberosKdcProxyMessage request = KerberosKdcProxyMessage.Decode(body);
        return Task.FromResult(ProxyReply ?? new KerberosKdcProxyMessage(Answer(request.KerberosMessage), null).Encode());
    }

    public byte[] Answer(byte[] bytes)
    {
        KerberosKdcRequest request = KerberosKdcRequest.Decode(bytes);
        Requests.Add(request);
        return Override(request) ?? (request.MessageType == KerberosMessageType.AsRequest ? AnswerAs(request) : AnswerTgs(request));
    }

    private void ThrowWhenUnreachable(string host)
    {
        if (UnreachableHosts.Contains(host))
        {
            throw new IOException($"{host} does not answer.");
        }
    }

    private byte[] AnswerAs(KerberosKdcRequest request)
    {
        KerberosPreAuthenticationData? timestamp = request.PreAuthenticationData.FirstOrDefault(data => data.DataType == KerberosPreAuthenticationData.EncryptedTimestamp);
        if (timestamp is null)
        {
            return RequirePreAuthentication ? Error(KerberosErrorMessage.PreAuthenticationRequired, MethodData()) : AsReply(request);
        }

        KerberosEncryptedData encrypted = KerberosEncryptedData.Decode(timestamp.Value);
        try
        {
            byte[] plaintext = KerberosEncryption.Create((KerberosEncryptionType)encrypted.EncryptionType, Confounders)
                .Decrypt(ClientKey(encrypted.EncryptionType, Salt), 1, encrypted.Cipher);
            LastTimestamp = KerberosEncryptedTimestamp.Decode(plaintext);
        }
        catch (KerberosCryptographyException)
        {
            return Error(24);
        }

        return AsReply(request);
    }

    private byte[] MethodData()
    {
        List<KerberosPreAuthenticationData> elements = [new(KerberosPreAuthenticationData.EncryptedTimestamp, [])];
        if (SendsKeyInfo)
        {
            elements.Insert(0, KeyInfo());
        }

        return KerberosPreAuthenticationData.EncodeMethodData(elements);
    }

    private KerberosPreAuthenticationData KeyInfo() =>
        new(KerberosPreAuthenticationData.EncryptionTypeInfo2, KerberosEncryptionTypeInfo2Entry.EncodeList([new(ClientKeyType, Salt, null)]));

    private byte[] AsReply(KerberosKdcRequest request)
    {
        KerberosEncryptedKdcReplyPart part = Part(KerberosMessageType.AsReply, TicketGrantingSessionKey, request.Body);
        byte[] cipher = KerberosEncryption.Create((KerberosEncryptionType)ClientKeyType, Confounders)
            .Encrypt(ClientKey(ClientKeyType, Salt), 3, part.Encode());
        return new KerberosKdcReply
        {
            MessageType = KerberosMessageType.AsReply,
            PreAuthenticationData = SendsKeyInfoInReply ? [KeyInfo()] : [],
            ClientRealm = Realm,
            ClientName = request.Body.ClientName!,
            Ticket = TicketFor(request.Body.ServerName!),
            EncryptedPart = new KerberosEncryptedData(ClientKeyType, 1, cipher),
        }.Encode();
    }

    private byte[] AnswerTgs(KerberosKdcRequest request)
    {
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, Confounders);
        KerberosApRequest apRequest = KerberosApRequest.Decode(request.PreAuthenticationData.Single(data => data.DataType == KerberosPreAuthenticationData.TgsRequest).Value);
        CollectionAssert.AreEqual(new[] { "krbtgt", Realm }, apRequest.Ticket.ServerName.Components.ToArray(), "PA-TGS-REQ must carry the ticket-granting ticket.");
        KerberosAuthenticator authenticator = KerberosAuthenticator.Decode(encryption.Decrypt(TicketGrantingSessionKey, 7, apRequest.Authenticator.Cipher));
        Assert.IsTrue(encryption.VerifyChecksum(TicketGrantingSessionKey, 6, request.Body.Encode(), authenticator.Checksum!.Value), "The authenticator must checksum the request body.");
        LastAuthenticator = authenticator;
        if (!request.Body.ServerName!.Components.SequenceEqual(Service.Components) && !request.Body.ServerName.Components.SequenceEqual(["krbtgt", Realm]))
        {
            return Error(7);
        }

        KerberosEncryptedKdcReplyPart part = Part(KerberosMessageType.TgsReply, ServiceSessionKey, request.Body);
        return new KerberosKdcReply
        {
            MessageType = KerberosMessageType.TgsReply,
            ClientRealm = authenticator.ClientRealm,
            ClientName = authenticator.ClientName,
            Ticket = TicketFor(request.Body.ServerName),
            EncryptedPart = new KerberosEncryptedData(18, null, encryption.Encrypt(TicketGrantingSessionKey, 8, part.Encode())),
        }.Encode();
    }

    private KerberosEncryptedKdcReplyPart Part(KerberosMessageType replyType, byte[] sessionKey, KerberosKdcRequestBody body) => new()
    {
        ReplyType = replyType,
        Key = new KerberosKey(18, [.. sessionKey]),
        LastRequests = [],
        Nonce = ReplyNonce(body.Nonce),
        Flags = body.Options.HasFlag(KerberosKdcOptions.Forwarded)
            ? KerberosTicketFlags.Forwarded | KerberosTicketFlags.Forwardable | KerberosTicketFlags.PreAuthenticated
            : KerberosTicketFlags.Initial | KerberosTicketFlags.PreAuthenticated,
        AuthenticationTime = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
        StartTime = ReplyStartTime,
        EndTime = new DateTimeOffset(2026, 9, 28, 22, 0, 0, TimeSpan.Zero),
        RenewUntil = ReplyRenewUntil,
        ClientAddresses = ReplyAddresses,
        ServerRealm = ReplyServerRealm ?? body.Realm,
        ServerName = ReplyServerName ?? body.ServerName!,
    };
}
