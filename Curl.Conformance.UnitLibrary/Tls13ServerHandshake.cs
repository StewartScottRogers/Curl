using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;

namespace Curl.Conformance;

/// <summary>
/// The server's answer to one TLS 1.3 ClientHello, built from Curl's own TLS codecs and key
/// schedule for <see cref="Tls13ServerStream"/>: TLS_AES_128_GCM_SHA256 with the client's X25519
/// key share (there is no HelloRetryRequest, so a client that shares no X25519 key is refused),
/// the first ALPN protocol of the server's list the client offers, and the certificate signed for
/// with its ECDSA key, as upstream's generated test certificates all are. It ignores Encrypted
/// Client Hello, as a server holding no ECH keys does, so a client that offered it learns from
/// this answer that ECH was rejected. It asks for no client certificate and resumes no session.
/// </summary>
internal sealed class Tls13ServerHandshake
{
    private Tls13ServerHandshake(byte[] serverHello, byte[] encryptedFlight, string? applicationProtocol)
    {
        ServerHello = serverHello;
        EncryptedFlight = encryptedFlight;
        ApplicationProtocol = applicationProtocol;
    }

    /// <summary>Gets the cipher suite every answer chooses.</summary>
    public static Tls13CipherSuite Suite { get; } = Tls13CipherSuite.Aes128GcmSha256;

    /// <summary>Gets the ServerHello handshake message, sent in plaintext.</summary>
    public byte[] ServerHello { get; }

    /// <summary>Gets EncryptedExtensions, Certificate, CertificateVerify and Finished, sent under the server handshake traffic secret.</summary>
    public byte[] EncryptedFlight { get; }

    /// <summary>Gets the ALPN protocol chosen, or <see langword="null"/> when none was.</summary>
    public string? ApplicationProtocol { get; }

    /// <summary>Gets the client handshake traffic secret, which protects the client's Finished.</summary>
    public byte[] ClientHandshakeTrafficSecret { get; private init; } = [];

    /// <summary>Gets the server handshake traffic secret, which protects <see cref="EncryptedFlight"/>.</summary>
    public byte[] ServerHandshakeTrafficSecret { get; private init; } = [];

    /// <summary>Gets the client application traffic secret.</summary>
    public byte[] ClientApplicationTrafficSecret { get; private init; } = [];

    /// <summary>Gets the server application traffic secret.</summary>
    public byte[] ServerApplicationTrafficSecret { get; private init; } = [];

    /// <summary>Gets the Finished handshake message the client must send.</summary>
    public byte[] ExpectedClientFinished { get; private init; } = [];

    /// <summary>Answers <paramref name="clientHelloMessage"/>, a whole ClientHello handshake message.</summary>
    /// <param name="clientHelloMessage">The ClientHello, header included, as the transcript hashes it.</param>
    /// <param name="options">The certificate and the ALPN protocols offered.</param>
    /// <returns>The answer and the secrets that follow from it.</returns>
    /// <exception cref="AuthenticationException">The hello shares no X25519 key, offers no signature scheme the certificate's key can make, or the certificate has no ECDSA key.</exception>
    public static Tls13ServerHandshake Answer(byte[] clientHelloMessage, TlsServerOptions options)
    {
        ClientHello hello = ClientHello.Decode(clientHelloMessage[HandshakeMessage.HeaderLength..]).Value;
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare(TlsNamedGroup.X25519);
        byte[] sharedSecret = share.ComputeSharedSecret(ClientX25519Share(hello))
            ?? throw new AuthenticationException("The client's X25519 key share is not a usable public key.");
        byte[] serverHello = new ServerHello(
            0x0303,
            RandomNumberGenerator.GetBytes(Curl.Tls.ClientHello.RandomLength),
            hello.LegacySessionId,
            Suite.Code,
            0,
            [KeyShareExtension.EncodeServerShare(share.Entry), SupportedVersionsExtension.EncodeSelected(0x0304)]).Encode();

        Tls13KeySchedule schedule = Suite.KeySchedule;
        using IncrementalHash transcript = IncrementalHash.CreateHash(schedule.HashAlgorithm);
        transcript.AppendData(clientHelloMessage);
        transcript.AppendData(serverHello);
        byte[] handshakeSecret = schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), sharedSecret);
        byte[] clientHandshakeTrafficSecret = schedule.DeriveClientHandshakeTrafficSecret(handshakeSecret, transcript.GetCurrentHash());
        byte[] serverHandshakeTrafficSecret = schedule.DeriveServerHandshakeTrafficSecret(handshakeSecret, transcript.GetCurrentHash());

        string? applicationProtocol = SelectApplicationProtocol(hello, options.ApplicationProtocols);
        List<byte> flight = [];
        Append(flight, transcript, new EncryptedExtensions(EncryptedExtensionsFor(applicationProtocol)).Encode());
        Append(flight, transcript, new CertificateMessage([], [new CertificateEntry(options.Certificate.RawData, [])]).Encode());
        Append(flight, transcript, CertificateVerifyFor(hello, options, transcript.GetCurrentHash()));
        Append(flight, transcript, new Finished(schedule.ComputeFinishedVerifyData(serverHandshakeTrafficSecret, transcript.GetCurrentHash())).Encode());

        byte[] serverFinishedHash = transcript.GetCurrentHash();
        byte[] masterSecret = schedule.ComputeMasterSecret(handshakeSecret);
        return new Tls13ServerHandshake(serverHello, [.. flight], applicationProtocol)
        {
            ClientHandshakeTrafficSecret = clientHandshakeTrafficSecret,
            ServerHandshakeTrafficSecret = serverHandshakeTrafficSecret,
            ClientApplicationTrafficSecret = schedule.DeriveClientApplicationTrafficSecret(masterSecret, serverFinishedHash),
            ServerApplicationTrafficSecret = schedule.DeriveServerApplicationTrafficSecret(masterSecret, serverFinishedHash),
            ExpectedClientFinished = new Finished(schedule.ComputeFinishedVerifyData(clientHandshakeTrafficSecret, serverFinishedHash)).Encode(),
        };
    }

    /// <summary>Checks the first record the client sent under its handshake keys: its Finished, or an alert that ends the handshake.</summary>
    /// <param name="content">The record's content and type.</param>
    /// <exception cref="AuthenticationException">The record is an alert, or is not the Finished the transcript calls for.</exception>
    public void CheckClientFinished(Tls13RecordContent content)
    {
        if (content.Type != TlsContentType.Handshake || !content.Content.AsSpan().SequenceEqual(ExpectedClientFinished))
        {
            throw new AuthenticationException(content.Type == TlsContentType.Alert
                ? $"The client ended the handshake with the {(TlsAlertDescription)content.Content[^1]} alert."
                : "The client did not send the Finished the transcript calls for.");
        }
    }

    private static byte[]? Find(ClientHello hello, TlsExtensionType type) =>
        hello.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static byte[] ClientX25519Share(ClientHello hello)
    {
        byte[] keyShares = Find(hello, TlsExtensionType.KeyShare) ?? [];
        TlsDecodeResult<IReadOnlyList<KeyShareEntry>> shares = KeyShareExtension.DecodeClientShares(keyShares);
        return (shares.Succeeded ? shares.Value : []).FirstOrDefault(entry => entry.Group == TlsNamedGroup.X25519)?.KeyExchange
            ?? throw new AuthenticationException("The client shares no X25519 key, and this server sends no HelloRetryRequest.");
    }

    private static string? SelectApplicationProtocol(ClientHello hello, IReadOnlyList<string> serverProtocols)
    {
        byte[]? offered = Find(hello, TlsExtensionType.ApplicationLayerProtocolNegotiation);
        IReadOnlyList<string> clientProtocols = offered is null ? [] : ApplicationLayerProtocolNegotiationExtension.Decode(offered).Value;
        return serverProtocols.FirstOrDefault(clientProtocols.Contains);
    }

    private static List<TlsExtension> EncryptedExtensionsFor(string? applicationProtocol) =>
        applicationProtocol is null ? [] : [ApplicationLayerProtocolNegotiationExtension.Encode([applicationProtocol])];

    private static byte[] CertificateVerifyFor(ClientHello hello, TlsServerOptions options, byte[] transcriptHash)
    {
        using ECDsa key = options.Certificate.GetECDsaPrivateKey()
            ?? throw new AuthenticationException("The server certificate has no ECDSA private key.");
        EcdsaTlsSigningKey signingKey = new(key);
        byte[] offered = Find(hello, TlsExtensionType.SignatureAlgorithms) ?? [];
        TlsDecodeResult<IReadOnlyList<ushort>> schemes = SignatureAlgorithmsExtension.Decode(offered);
        ushort scheme = (schemes.Succeeded ? schemes.Value : []).FirstOrDefault(signingKey.CanSign);
        if (scheme == 0)
        {
            throw new AuthenticationException("The client offers no signature scheme the certificate's key can make.");
        }

        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(true, transcriptHash);
        return new CertificateVerify(scheme, signingKey.Sign(scheme, content)).Encode();
    }

    private static void Append(List<byte> flight, IncrementalHash transcript, byte[] message)
    {
        flight.AddRange(message);
        transcript.AppendData(message);
    }
}
