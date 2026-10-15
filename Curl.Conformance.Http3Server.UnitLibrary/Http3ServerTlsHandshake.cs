using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// The HTTP/3 stand-in's I/O-free TLS 1.3 server handshake, at the message level QUIC carries
/// in CRYPTO frames (RFC 9001 section 4): no TLS records. <see cref="Receive" /> takes the
/// client's handshake bytes at an encryption level and returns the server's bytes to send per
/// level and the traffic secrets installed per level and direction, or the alert that ends it.
/// It answers a ClientHello with ServerHello (or a HelloRetryRequest when the client shared no
/// key for a common group), then EncryptedExtensions (ALPN <c>h3</c> and the caller's
/// <c>quic_transport_parameters</c>), Certificate, CertificateVerify and Finished, and checks
/// the client's Finished.
/// </summary>
/// <param name="settings">The certificate, key, transport parameters and preferences.</param>
/// <param name="random">The source of the server random and the server's key share.</param>
public sealed class Http3ServerTlsHandshake(Http3ServerTlsSettings settings, ITlsRandomSource random) : IDisposable
{
    private const ushort LegacyVersion = 0x0303;

    private const ushort Tls13 = 0x0304;

    private readonly Http3ServerTlsSettings settings = settings ?? throw new ArgumentNullException(nameof(settings));

    private readonly ITlsRandomSource random = random ?? throw new ArgumentNullException(nameof(random));

    private readonly List<byte> pending = [];

    private State state;

    private TranscriptHash? transcript;

    private ushort? retryGroup;

    private byte[] clientHandshakeSecret = [];

    private byte[] clientApplicationSecret = [];

    private enum State
    {
        AwaitingClientHello,
        AwaitingClientFinished,
        Complete,
        Failed,
    }

    /// <summary>Gets a value indicating whether the client's Finished has been verified.</summary>
    public bool IsComplete => state == State.Complete;

    /// <summary>Gets the alert that ended the handshake, or <see langword="null" />.</summary>
    public TlsHandshakeFailure? Failure { get; private set; }

    /// <summary>Gets the cipher suite chosen, once a ClientHello has been read.</summary>
    public Tls13CipherSuite? CipherSuite { get; private set; }

    /// <summary>Gets the key exchange group of the ServerHello, once sent.</summary>
    public ushort? NegotiatedGroup { get; private set; }

    /// <summary>Gets the CertificateVerify signature scheme, once sent.</summary>
    public ushort? SignatureScheme { get; private set; }

    /// <summary>Gets the client's encoded <c>quic_transport_parameters</c>, once its ClientHello has been read.</summary>
    public byte[]? ClientQuicTransportParameters { get; private set; }

    /// <summary>
    /// Takes the client's handshake bytes at <paramref name="level" /> - the ClientHello at
    /// Initial, the Finished at Handshake - whole messages or parts of them.
    /// </summary>
    /// <param name="level">The encryption level the bytes arrived at.</param>
    /// <param name="bytes">The handshake bytes.</param>
    /// <returns>The bytes to send and secrets installed, or the failure with its alert.</returns>
    /// <exception cref="InvalidOperationException">The handshake has already completed or failed.</exception>
    public Tls13HandshakeOutput Receive(TlsEncryptionLevel level, ReadOnlySpan<byte> bytes)
    {
        if (state is State.Complete or State.Failed)
        {
            throw new InvalidOperationException("The handshake has already ended.");
        }

        Http3ServerTlsOutput output = new();
        pending.AddRange(bytes);
        try
        {
            while (TryTakeMessage(out HandshakeMessage? message))
            {
                Http3ServerClientOffer.Require(level == ExpectedLevel, TlsAlertDescription.UnexpectedMessage);
                Dispatch(message!, output);
            }
        }
        catch (TlsAlertException alert)
        {
            state = State.Failed;
            Failure = new TlsHandshakeFailure(alert.Alert, null);
            return new Tls13HandshakeOutput([], [], false, Failure);
        }

        return output.Build(IsComplete);
    }

    /// <inheritdoc />
    public void Dispose() => transcript?.Dispose();

    private TlsEncryptionLevel ExpectedLevel => state == State.AwaitingClientHello ? TlsEncryptionLevel.Initial : TlsEncryptionLevel.Handshake;

    private static byte[] Concatenate(params byte[][] messages) => [.. messages.SelectMany(message => message)];

    private bool TryTakeMessage(out HandshakeMessage? message)
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(CollectionsMarshal.AsSpan(pending));
        Http3ServerClientOffer.Require(read.Status != HandshakeMessageReadStatus.Failed, read.Alert.GetValueOrDefault());
        message = read.Message;
        pending.RemoveRange(0, read.BytesConsumed);
        return message is not null;
    }

    private void Dispatch(HandshakeMessage message, Http3ServerTlsOutput output)
    {
        Http3ServerClientOffer.Require(state != State.Complete, TlsAlertDescription.UnexpectedMessage);
        if (state == State.AwaitingClientHello)
        {
            ReceiveClientHello(message, output);
        }
        else
        {
            ReceiveClientFinished(message, output);
        }
    }

    private void ReceiveClientHello(HandshakeMessage message, Http3ServerTlsOutput output)
    {
        Http3ServerClientOffer.Require(message.Type == HandshakeType.ClientHello, TlsAlertDescription.UnexpectedMessage);
        TlsDecodeResult<ClientHello> decoded = ClientHello.Decode(message.Body);
        Http3ServerClientOffer.Require(decoded.Succeeded, decoded.Alert.GetValueOrDefault());
        ClientHello hello = decoded.Value;
        Http3ServerClientOffer offer = Http3ServerClientOffer.Parse(hello);
        Tls13CipherSuite suite = settings.CipherSuites
            .Where(hello.CipherSuites.Contains)
            .Select(Tls13CipherSuite.Find)
            .FirstOrDefault(found => found is not null) ?? throw new TlsAlertException(TlsAlertDescription.HandshakeFailure, isFromServer: true);
        ushort scheme = ChooseSignatureScheme(offer);
        KeyShareEntry? share = ChooseKeyShare(offer);
        CipherSuite = suite;
        ClientQuicTransportParameters = offer.QuicTransportParameters;
        transcript ??= suite.KeySchedule.CreateTranscriptHash();
        transcript.Append(message.Encode());
        if (share is null)
        {
            SendHelloRetryRequest(hello, suite, output);
        }
        else
        {
            SendServerFlight(hello, suite, share, scheme, output);
        }
    }

    private ushort ChooseSignatureScheme(Http3ServerClientOffer offer)
    {
        ushort? scheme = settings.SignatureSchemes
            .Where(candidate => offer.SignatureSchemes.Contains(candidate) && settings.SigningKey.CanSign(candidate))
            .Select(candidate => (ushort?)candidate)
            .FirstOrDefault();
        Http3ServerClientOffer.Require(scheme is not null, TlsAlertDescription.HandshakeFailure);
        return scheme!.Value;
    }

    // The first common group the client shared a key for; with none, a HelloRetryRequest for
    // the server's most preferred common group, which a second ClientHello must then share.
    private KeyShareEntry? ChooseKeyShare(Http3ServerClientOffer offer)
    {
        ushort[] common = [.. settings.Groups.Where(offer.Groups.Contains)];
        Http3ServerClientOffer.Require(common.Length > 0, TlsAlertDescription.HandshakeFailure);
        KeyShareEntry? share = common
            .Select(group => offer.KeyShares.FirstOrDefault(entry => entry.Group == group))
            .FirstOrDefault(entry => entry is not null);
        Http3ServerClientOffer.Require(share is not null || retryGroup is null, TlsAlertDescription.IllegalParameter);
        retryGroup = common[0];
        return share;
    }

    private void SendHelloRetryRequest(ClientHello hello, Tls13CipherSuite suite, Http3ServerTlsOutput output)
    {
        transcript!.ReplaceWithMessageHash();
        byte[] retry = new ServerHello(
            LegacyVersion,
            ServerHello.HelloRetryRequestRandom.ToArray(),
            hello.LegacySessionId,
            suite.Code,
            0,
            [SupportedVersionsExtension.EncodeSelected(Tls13), KeyShareExtension.EncodeSelectedGroup(retryGroup!.Value)]).Encode();
        transcript.Append(retry);
        output.Send(TlsEncryptionLevel.Initial, retry);
    }

    private void SendServerFlight(ClientHello hello, Tls13CipherSuite suite, KeyShareEntry share, ushort scheme, Http3ServerTlsOutput output)
    {
        Tls13KeySchedule schedule = suite.KeySchedule;
        using Tls13KeyShare serverShare = random.CreateKeyShare(share.Group);
        byte[] sharedSecret = serverShare.ComputeSharedSecret(share.KeyExchange)
            ?? throw new TlsAlertException(TlsAlertDescription.IllegalParameter, isFromServer: true);
        byte[] serverRandom = new byte[ClientHello.RandomLength];
        random.Fill(serverRandom);
        byte[] serverHello = new ServerHello(
            LegacyVersion,
            serverRandom,
            hello.LegacySessionId,
            suite.Code,
            0,
            [SupportedVersionsExtension.EncodeSelected(Tls13), KeyShareExtension.EncodeServerShare(serverShare.Entry)]).Encode();
        transcript!.Append(serverHello);
        output.Send(TlsEncryptionLevel.Initial, serverHello);
        NegotiatedGroup = share.Group;

        byte[] handshakeSecret = schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), sharedSecret);
        byte[] helloHash = transcript.GetCurrentHash();
        clientHandshakeSecret = schedule.DeriveClientHandshakeTrafficSecret(handshakeSecret, helloHash);
        byte[] serverHandshakeSecret = schedule.DeriveServerHandshakeTrafficSecret(handshakeSecret, helloHash);
        output.Install(TlsEncryptionLevel.Handshake, TlsTrafficDirection.Read, clientHandshakeSecret);
        output.Install(TlsEncryptionLevel.Handshake, TlsTrafficDirection.Write, serverHandshakeSecret);

        output.Send(TlsEncryptionLevel.Handshake, BuildEncryptedFlight(schedule, scheme, serverHandshakeSecret));
        SignatureScheme = scheme;

        byte[] masterSecret = schedule.ComputeMasterSecret(handshakeSecret);
        byte[] finishedHash = transcript.GetCurrentHash();
        clientApplicationSecret = schedule.DeriveClientApplicationTrafficSecret(masterSecret, finishedHash);
        output.Install(TlsEncryptionLevel.Application, TlsTrafficDirection.Write, schedule.DeriveServerApplicationTrafficSecret(masterSecret, finishedHash));
        state = State.AwaitingClientFinished;
    }

    // EncryptedExtensions, Certificate, CertificateVerify and Finished, each added to the
    // transcript in turn, as one run of Handshake-level bytes.
    private byte[] BuildEncryptedFlight(Tls13KeySchedule schedule, ushort scheme, byte[] serverHandshakeSecret)
    {
        byte[] encryptedExtensions = new EncryptedExtensions(
        [
            ApplicationLayerProtocolNegotiationExtension.Encode([Http3ServerTlsSettings.ApplicationProtocol]),
            QuicTransportParametersExtension.Encode(settings.QuicTransportParameters),
        ]).Encode();
        transcript!.Append(encryptedExtensions);
        byte[] certificate = new CertificateMessage([], [.. settings.CertificateChain.Select(der => new CertificateEntry(der, []))]).Encode();
        transcript.Append(certificate);
        byte[] signed = TlsSignatureScheme.BuildCertificateVerifyContent(server: true, transcript.GetCurrentHash());
        byte[] certificateVerify = new CertificateVerify(scheme, settings.SigningKey.Sign(scheme, signed)).Encode();
        transcript.Append(certificateVerify);
        byte[] finished = new Finished(schedule.ComputeFinishedVerifyData(serverHandshakeSecret, transcript.GetCurrentHash())).Encode();
        transcript.Append(finished);
        return Concatenate(encryptedExtensions, certificate, certificateVerify, finished);
    }

    private void ReceiveClientFinished(HandshakeMessage message, Http3ServerTlsOutput output)
    {
        Http3ServerClientOffer.Require(message.Type == HandshakeType.Finished, TlsAlertDescription.UnexpectedMessage);
        byte[] expected = CipherSuite!.KeySchedule.ComputeFinishedVerifyData(clientHandshakeSecret, transcript!.GetCurrentHash());
        Http3ServerClientOffer.Require(CryptographicOperations.FixedTimeEquals(expected, message.Body), TlsAlertDescription.DecryptError);
        transcript.Append(message.Encode());
        output.Install(TlsEncryptionLevel.Application, TlsTrafficDirection.Read, clientApplicationSecret);
        state = State.Complete;
    }
}
