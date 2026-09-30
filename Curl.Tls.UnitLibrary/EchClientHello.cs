using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// One Encrypted Client Hello offer (RFC 9849): the inner ClientHello, which names the real
/// server, sealed with HPKE into the outer one, which names <see cref="EchConfig.PublicName" />;
/// the check of the server's acceptance confirmation in its HelloRetryRequest and ServerHello;
/// and which of the two hellos the handshake then continues with. It also builds the GREASE
/// extension a client without a config sends (section 6.2).
/// </summary>
/// <remarks>
/// Both hellos carry the same key shares, cipher suites and legacy session ID, so the
/// ServerHello works with the client's shares whichever the server answered. The inner
/// hello offers TLS 1.3 alone (section 6.1) and is sent whole, with no
/// <c>ech_outer_extensions</c>; it is padded by section 6.1.3's rule. A resuming inner hello
/// carries the ticket, and the outer one a GREASE <c>pre_shared_key</c> of the same lengths
/// (section 6.1.2), with <c>early_data</c> only when the inner one has it.
/// </remarks>
internal sealed class EchClientHello : IDisposable
{
    private const int ServerHelloRandomOffset = HandshakeMessage.HeaderLength + 2;

    private const int ConfirmationOffset = ServerHelloRandomOffset + ClientHello.RandomLength - EncryptedClientHelloExtension.ConfirmationLength;

    private const int GreasePrivateKeyLength = 32;

    private readonly EchCipherSuite suite;
    private readonly HpkeContext context;
    private readonly ITlsRandomSource random;
    private readonly Tls13ClientHelloBuilder innerBuilder;
    private readonly Tls13ClientHelloBuilder outerBuilder;
    private readonly string? serverName;
    private byte[] encapsulatedKey;
    private ClientHello? inner;
    private ClientHello? outer;
    private bool acceptedAtRetry;

    private EchClientHello(
        Tls13ClientSettings settings,
        EchConfig config,
        EchCipherSuite suite,
        HpkeContext context,
        ITlsRandomSource random,
        byte[] encapsulatedKey,
        byte[] innerRandom,
        byte[] outerRandom,
        byte[] legacySessionId)
    {
        Config = config;
        this.suite = suite;
        this.context = context;
        this.random = random;
        this.encapsulatedKey = encapsulatedKey;
        serverName = settings.ServerName;
        innerBuilder = new Tls13ClientHelloBuilder(InnerSettings(settings), innerRandom, legacySessionId);
        outerBuilder = new Tls13ClientHelloBuilder(settings with { ServerName = config.PublicName }, outerRandom, legacySessionId);
    }

    /// <summary>Gets the config the offer seals to.</summary>
    public EchConfig Config { get; }

    /// <summary>Gets a value indicating whether the server's ServerHello confirmed it decrypted the inner hello.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Gets a value indicating whether the server answered the outer hello instead: the handshake continues with it, under <see cref="EchConfig.PublicName" />.</summary>
    public bool Rejected { get; private set; }

    /// <summary>Gets the hello the transcript holds: the inner one until a rejection, then the outer one.</summary>
    public ClientHello TranscriptHello => Rejected ? outer! : inner!;

    /// <summary>
    /// Sets up the offer to <paramref name="config" />, a
    /// <see cref="EchConfigList.SupportedConfig" />, drawing the inner hello's random and the HPKE ephemeral
    /// key, in that order, from <paramref name="random" />.
    /// </summary>
    public static EchClientHello Create(
        Tls13ClientSettings settings,
        EchConfig config,
        ITlsRandomSource random,
        byte[] outerRandom,
        byte[] legacySessionId)
    {
        EchCipherSuite suite = config.FindSupportedSuite()!.Value;
        byte[] innerRandom = new byte[ClientHello.RandomLength];
        random.Fill(innerRandom);
        byte[] ephemeralPrivateKey = new byte[Hpke.PrivateKeySize];
        random.Fill(ephemeralPrivateKey);
        HpkeKem kem = (HpkeKem)config.KemId;
        byte[] encapsulatedKey = new byte[Hpke.GetEncapsulatedKeySize(kem)];
        byte[] info = [.. "tls ech"u8, 0x00, .. config.Encoded];

        // SupportedConfig has already encapsulated to this public key once, so only an
        // ephemeral scalar outside the curve's order (a chance of about 2^-32 on P-256) fails here.
        _ = Hpke.TrySetupBaseSender(kem, (HpkeKdf)suite.KdfId, (HpkeAead)suite.AeadId, config.PublicKey, ephemeralPrivateKey, info, encapsulatedKey, out HpkeContext? context);
        CryptographicOperations.ZeroMemory(ephemeralPrivateKey);
        return new EchClientHello(settings, config, suite, context!, random, encapsulatedKey, innerRandom, outerRandom, legacySessionId);
    }

    /// <summary>
    /// Returns a GREASE <c>encrypted_client_hello</c> (RFC 9849 section 6.2): a random
    /// <c>config_id</c>, <see cref="EchCipherSuite.Grease" />, a valid X25519 <c>enc</c> and a
    /// random payload the length a real one would have for the inner hello these settings
    /// build with a <c>maximum_name_length</c> of 0, drawn from <paramref name="random" /> in that order.
    /// </summary>
    public static TlsExtension CreateGrease(
        Tls13ClientSettings settings,
        ITlsRandomSource random,
        IReadOnlyList<KeyShareEntry> shares,
        byte[] clientRandom,
        byte[] legacySessionId)
    {
        byte[] configId = new byte[1];
        random.Fill(configId);
        byte[] privateKey = new byte[GreasePrivateKeyLength];
        random.Fill(privateKey);
        byte[] encapsulatedKey = new byte[X25519.KeySize];
        X25519.ComputePublicKey(privateKey, encapsulatedKey);
        CryptographicOperations.ZeroMemory(privateKey);
        ClientHello wouldBeInner = new Tls13ClientHelloBuilder(InnerSettings(settings), clientRandom, legacySessionId)
            .Build(shares, null, null, EncryptedClientHelloExtension.EncodeInner());
        byte[] payload = new byte[EncodeInner(wouldBeInner, 0, settings.ServerName).Length + HpkeContext.TagSize];
        random.Fill(payload);
        return EncryptedClientHelloExtension.EncodeOuter(EchCipherSuite.Grease, configId[0], encapsulatedKey, payload);
    }

    /// <summary>
    /// Builds the inner hello and the outer hello that carries it sealed, and returns the
    /// outer one to send. After a HelloRetryRequest the sealing context runs on and
    /// <c>enc</c> is empty (section 6.1.5). With <paramref name="pskOffer" /> the inner hello
    /// offers the ticket, its binder filled in by <paramref name="bindPsk" /> over the inner
    /// transcript, and the outer hello a GREASE one (section 6.1.2).
    /// </summary>
    public ClientHello Build(IReadOnlyList<KeyShareEntry> shares, byte[]? cookie, Tls13PskOffer? pskOffer, Func<ClientHello, ClientHello> bindPsk)
    {
        inner = bindPsk(innerBuilder.Build(shares, cookie, pskOffer, EncryptedClientHelloExtension.EncodeInner()));
        byte[] encodedInner = EncodeInner(inner, Config.MaximumNameLength, serverName);
        byte[] payload = new byte[encodedInner.Length + HpkeContext.TagSize];
        ClientHello associatedData = BuildOuter(shares, cookie, pskOffer, EncryptedClientHelloExtension.EncodeOuter(suite, Config.ConfigId, encapsulatedKey, payload));
        context.Seal(associatedData.Encode().AsSpan(HandshakeMessage.HeaderLength), encodedInner, payload);
        TlsExtension sealedExtension = EncryptedClientHelloExtension.EncodeOuter(suite, Config.ConfigId, encapsulatedKey, payload);
        outer = associatedData with
        {
            Extensions = [.. associatedData.Extensions.Select(extension => extension.Type == TlsExtensionType.EncryptedClientHello ? sealedExtension : extension)],
        };
        encapsulatedKey = [];
        return outer;
    }

    /// <summary>
    /// Reads a HelloRetryRequest's confirmation (section 7.2.1): present and right, the
    /// server took the inner hello; absent or wrong, it rejected ECH.
    /// </summary>
    /// <returns><see cref="TlsAlertDescription.DecodeError" /> for a confirmation that is not 8 bytes, otherwise <see langword="null" />.</returns>
    public TlsAlertDescription? ReceiveHelloRetryRequest(ServerHello retry, Tls13KeySchedule schedule)
    {
        byte[]? data = retry.Extensions.FirstOrDefault(extension => extension.Type == TlsExtensionType.EncryptedClientHello)?.Data;
        if (data is null)
        {
            Rejected = true;
            return null;
        }

        TlsDecodeResult<byte[]> confirmation = EncryptedClientHelloExtension.DecodeRetryConfirmation(data);
        if (!confirmation.Succeeded)
        {
            return confirmation.Alert;
        }

        TlsExtension zeroed = new(TlsExtensionType.EncryptedClientHello, new byte[EncryptedClientHelloExtension.ConfirmationLength]);
        ServerHello withoutConfirmation = retry with
        {
            Extensions = [.. retry.Extensions.Select(extension => extension.Type == TlsExtensionType.EncryptedClientHello ? zeroed : extension)],
        };
        using TranscriptHash hash = schedule.CreateTranscriptHash();
        hash.Append(inner!.Encode());
        hash.ReplaceWithMessageHash();
        hash.Append(withoutConfirmation.Encode());
        acceptedAtRetry = CryptographicOperations.FixedTimeEquals(Confirm(schedule, "hrr ech accept confirmation", hash.GetCurrentHash()), confirmation.Value);
        Rejected = !acceptedAtRetry;
        return null;
    }

    /// <summary>
    /// Reads the ServerHello's confirmation, the last 8 bytes of its random (section 7.2):
    /// right, ECH is accepted; wrong, rejected. A server that confirmed in its
    /// HelloRetryRequest and not here is <see cref="TlsAlertDescription.IllegalParameter" />.
    /// </summary>
    /// <param name="encoded">The ServerHello with its header.</param>
    /// <param name="schedule">The key schedule of the suite the server chose.</param>
    /// <param name="transcript">The transcript after a HelloRetryRequest, or <see langword="null" /> without one.</param>
    public TlsAlertDescription? ReceiveServerHello(byte[] encoded, Tls13KeySchedule schedule, TranscriptHash? transcript)
    {
        if (Rejected)
        {
            return null;
        }

        byte[] expected = Confirm(schedule, "ech accept confirmation", HashWithoutConfirmation(encoded, schedule, transcript));
        Accepted = CryptographicOperations.FixedTimeEquals(expected, encoded.AsSpan(ConfirmationOffset, EncryptedClientHelloExtension.ConfirmationLength));
        Rejected = !Accepted;
        return Rejected && acceptedAtRetry ? TlsAlertDescription.IllegalParameter : null;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    /// <summary>
    /// Returns the <c>EncodedClientHelloInner</c> (section 5.1): the hello without its header
    /// and with an empty legacy session ID, then zeros - <c>maximum_name_length</c> less the
    /// name's length (or plus 9 with no name), then up to a multiple of 32 (section 6.1.3).
    /// </summary>
    private static byte[] EncodeInner(ClientHello hello, int maximumNameLength, string? serverName)
    {
        byte[] body = (hello with { LegacySessionId = [] }).Encode()[HandshakeMessage.HeaderLength..];
        int padding = serverName is null ? maximumNameLength + 9 : Math.Max(0, maximumNameLength - Encoding.Latin1.GetByteCount(serverName));
        padding += 31 - ((body.Length + padding - 1) % 32);
        return [.. body, .. new byte[padding]];
    }

    /// <summary>
    /// <c>transcript_ech_conf</c> (section 7.2): the transcript so far - the inner hello, or
    /// after a HelloRetryRequest <paramref name="transcript" /> - then the ServerHello with the
    /// last 8 bytes of its random zeroed.
    /// </summary>
    private byte[] HashWithoutConfirmation(byte[] encoded, Tls13KeySchedule schedule, TranscriptHash? transcript)
    {
        byte[] withoutConfirmation = [.. encoded];
        withoutConfirmation.AsSpan(ConfirmationOffset, EncryptedClientHelloExtension.ConfirmationLength).Clear();
        using TranscriptHash hash = transcript?.Clone() ?? schedule.CreateTranscriptHash();
        if (transcript is null)
        {
            hash.Append(inner!.Encode());
        }

        hash.Append(withoutConfirmation);
        return hash.GetCurrentHash();
    }

    /// <summary>
    /// Builds the outer hello around <paramref name="echExtension" />. When the inner hello
    /// offers a ticket, the outer one carries a GREASE <c>pre_shared_key</c> (section 6.1.2): a
    /// random identity as long as the ticket, a random obfuscated age and a random binder as
    /// long as the real one, drawn from the random source in that order, with <c>early_data</c>
    /// only when the inner hello asks for it.
    /// </summary>
    private ClientHello BuildOuter(IReadOnlyList<KeyShareEntry> shares, byte[]? cookie, Tls13PskOffer? pskOffer, TlsExtension echExtension)
    {
        if (pskOffer is null)
        {
            return outerBuilder.Build(shares, cookie, null, echExtension);
        }

        byte[] identity = new byte[pskOffer.Identity.Identity.Length];
        random.Fill(identity);
        byte[] age = new byte[sizeof(uint)];
        random.Fill(age);
        byte[] binder = new byte[pskOffer.BinderLength];
        random.Fill(binder);
        Tls13PskOffer grease = pskOffer with { Identity = new PskIdentity(identity, BinaryPrimitives.ReadUInt32BigEndian(age)) };
        ClientHello hello = outerBuilder.Build(shares, cookie, grease, echExtension);
        TlsExtension offered = PreSharedKeyExtension.EncodeOffered(new OfferedPsks([grease.Identity], [binder]));
        return hello with { Extensions = [.. hello.Extensions.SkipLast(1), offered] };
    }

    /// <summary>The inner hello offers TLS 1.3 alone (section 6.1).</summary>
    private static Tls13ClientSettings InnerSettings(Tls13ClientSettings settings) => settings with { LowerVersions = null };

    /// <summary>HKDF-Expand-Label(HKDF-Extract(0, ClientHelloInner.random), <paramref name="label" />, <paramref name="transcriptHash" />, 8).</summary>
    private byte[] Confirm(Tls13KeySchedule schedule, string label, byte[] transcriptHash) =>
        schedule.ExpandLabel(schedule.Extract(new byte[schedule.HashLength], inner!.Random), label, transcriptHash, EncryptedClientHelloExtension.ConfirmationLength);
}
