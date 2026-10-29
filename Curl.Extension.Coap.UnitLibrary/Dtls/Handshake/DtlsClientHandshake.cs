using System.Security.Cryptography;
using Curl.Extension.Coap.Client;
using Curl.Extension.Coap.Dtls.Record;
using Curl.Extension.Coap.Dtls.Retransmission;
using Curl.Extension.Coap.Dtls.Wire;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using TlsContentType = Curl.Tls.TlsContentType;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// Runs a DTLS 1.2 client handshake over a datagram channel (RFC 6347 section 4.2, RFC 5246 section
/// 7.3): a ClientHello, again with the cookie of a HelloVerifyRequest; the server's ServerHello,
/// Certificate, ServerKeyExchange and ServerHelloDone; the chain checked by the transfer's
/// <see cref="ITlsProvider.VerifyServerCertificateChain" /> and the ECDHE parameters' signature by its
/// leaf; then ClientKeyExchange, ChangeCipherSpec and Finished, and the server's Finished checked. Each
/// flight is resent by <see cref="DtlsFlightRetransmitter" />. A rejected chain fails with the verdict's
/// exit code (60 for an untrusted one); every other failure, giving up after the retransmissions
/// included, with exit 35.
/// </summary>
internal sealed class DtlsClientHandshake(IDatagramChannel channel, ITlsProvider tlsProvider, string targetHost, TimeProvider timeProvider)
{
    private const int MaximumDatagramLength = 65535;

    private const int RandomLength = 32;

    private static readonly HandshakeType[] ServerHelloFlight =
        [HandshakeType.ServerHello, HandshakeType.Certificate, HandshakeType.ServerKeyExchange, HandshakeType.ServerHelloDone];

    private readonly DtlsFlightRetransmitter retransmitter = new((datagram, cancellationToken) => channel.SendAsync(datagram, channel.ServerEndPoint, cancellationToken), timeProvider);

    private readonly DtlsHandshakeMessageReader reader = new();

    private readonly List<byte> transcript = [];

    private readonly byte[] clientRandom = RandomNumberGenerator.GetBytes(RandomLength);

    private readonly byte[] buffer = new byte[MaximumDatagramLength];

    private DtlsRecordOpener openRecord = OpenEpochZeroRecord;

    private ulong epochZeroSequenceNumber;

    private ushort nextMessageSequence;

    private delegate bool DtlsRecordOpener(ReadOnlySpan<byte> record, out DtlsRecordHeader header, out byte[] plaintext);

    /// <summary>Runs the handshake.</summary>
    /// <returns>The session that protects application data under the negotiated keys.</returns>
    /// <exception cref="CoapException">The handshake failed; <see cref="CoapException.ExitCode" /> says why.</exception>
    public async ValueTask<DtlsClientSession> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<DtlsHandshakeMessage> serverFlight = await ExchangeHellosAsync(cancellationToken).ConfigureAwait(false);
            return await ExchangeKeysAsync(serverFlight, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new CoapException(CurlExitCode.SslConnectError, exception.Message);
        }
        catch (FormatException exception)
        {
            throw new CoapException(CurlExitCode.SslConnectError, $"DTLS handshake message does not parse: {exception.Message}");
        }
    }

    private static bool OpenEpochZeroRecord(ReadOnlySpan<byte> record, out DtlsRecordHeader header, out byte[] plaintext)
    {
        header = DtlsRecordHeader.Decode(record);
        plaintext = record[DtlsRecordHeader.HeaderLength..].ToArray();
        return header.Epoch == 0;
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new CoapException(CurlExitCode.SslConnectError, $"DTLS handshake failed: {failure}");
        }
    }

    private static T Decoded<T>(TlsDecodeResult<T> result, HandshakeType messageType)
    {
        Require(result.Succeeded, $"the server's {messageType} does not parse");
        return result.Value;
    }

    /// <summary>Sends the ClientHello, again with each HelloVerifyRequest's cookie, until the server answers with its hello flight (RFC 6347 section 4.2.1).</summary>
    private async ValueTask<List<DtlsHandshakeMessage>> ExchangeHellosAsync(CancellationToken cancellationToken)
    {
        byte[] cookie = [];
        for (int verifyRequests = 0; ; verifyRequests++)
        {
            DtlsHandshakeMessage hello = new(HandshakeType.ClientHello, nextMessageSequence++, DtlsClientHello.Encode(clientRandom, cookie, targetHost));
            List<DtlsHandshakeMessage> flight = await SendFlightAsync(
                [Epoch0Record(TlsContentType.Handshake, hello.TranscriptBytes())],
                type => type is DtlsHelloVerifyRequest.MessageType or HandshakeType.ServerHelloDone,
                cancellationToken).ConfigureAwait(false);
            if (flight[^1].MessageType != DtlsHelloVerifyRequest.MessageType)
            {
                // The transcript starts at the ClientHello the server answered: section 4.2.6 leaves out the first and the HelloVerifyRequest.
                transcript.AddRange(hello.TranscriptBytes());
                return flight;
            }

            Require(verifyRequests == 0, "the server sent a second HelloVerifyRequest");
            cookie = DtlsHelloVerifyRequest.Decode(flight[^1].Body).Cookie;
        }
    }

    /// <summary>Checks the server's hello flight, then sends the key exchange and Finished and checks the server's Finished (RFC 5246 sections 7.4.1.3 to 7.4.9).</summary>
    private async ValueTask<DtlsClientSession> ExchangeKeysAsync(List<DtlsHandshakeMessage> serverFlight, CancellationToken cancellationToken)
    {
        Require(serverFlight.Select(message => message.MessageType).SequenceEqual(ServerHelloFlight), "the server's hello flight is not ServerHello, Certificate, ServerKeyExchange and ServerHelloDone");
        foreach (DtlsHandshakeMessage message in serverFlight)
        {
            transcript.AddRange(message.TranscriptBytes());
        }

        ServerHello hello = Decoded(ServerHello.Decode(serverFlight[0].Body), HandshakeType.ServerHello);
        Require(hello.LegacyVersion == DtlsRecordHeader.Dtls12Version, "the server did not choose DTLS 1.2");
        Require(DtlsClientHello.CipherSuites.Contains(hello.CipherSuite), $"the server chose cipher suite 0x{hello.CipherSuite:X4}, which the client did not offer");
        byte[] leaf = VerifyCertificateChain(Decoded(Tls12CertificateMessage.Decode(serverFlight[1].Body), HandshakeType.Certificate));
        Tls12ServerKeyExchange keyExchange = Decoded(Tls12ServerKeyExchange.Decode(serverFlight[2].Body, Tls12KeyExchange.Ecdhe, true, true), HandshakeType.ServerKeyExchange);
        Tls12EcdheParameters parameters = (Tls12EcdheParameters)keyExchange.Parameters;
        Require(parameters.NamedGroup == TlsNamedGroup.Secp256r1, $"the server chose group 0x{parameters.NamedGroup:X4}, which the client did not offer");
        using EcdhKeyShare clientShare = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        byte[]? preMasterSecret = clientShare.ComputeSharedSecret(parameters.PublicKey);
        Require(preMasterSecret is not null, "the server's ECDHE public value is not a point on secp256r1");
        Require(
            DtlsServerKeyExchangeSignature.Verify(leaf, keyExchange.SignatureAlgorithm!.Value, keyExchange.BuildSignedContent(clientRandom, hello.Random), keyExchange.Signature!),
            "the server's ServerKeyExchange signature does not verify with its certificate's key");
        byte[] clientKeyExchange = new Tls12ClientKeyExchange(Tls12KeyExchange.Ecdhe, [(byte)clientShare.PublicKey.Length, .. clientShare.PublicKey]).Encode()[HandshakeMessage.HeaderLength..];
        byte[] keyExchangeFragment = new DtlsHandshakeMessage(HandshakeType.ClientKeyExchange, nextMessageSequence++, clientKeyExchange).TranscriptBytes();
        transcript.AddRange(keyExchangeFragment);
        // RFC 7627 section 4: the session hash covers the transcript through ClientKeyExchange.
        byte[] masterSecret = hello.Extensions.Any(extension => extension.Type == TlsExtensionType.ExtendedMasterSecret)
            ? TlsPrf.Sha256.ComputeExtendedMasterSecret(preMasterSecret!, SHA256.HashData([.. transcript]))
            : TlsPrf.Sha256.ComputeMasterSecret(preMasterSecret!, clientRandom, hello.Random);
        return await FinishAsync(masterSecret, DtlsAes128GcmKeyBlock.Derive(masterSecret, clientRandom, hello.Random), keyExchangeFragment, cancellationToken).ConfigureAwait(false);
    }

    private byte[] VerifyCertificateChain(Tls12CertificateMessage certificate)
    {
        Require(certificate.CertificateList.Count > 0, "the server sent no certificate");
        ServerCertificateChainVerdict verdict = tlsProvider.VerifyServerCertificateChain([.. certificate.CertificateList.Select(der => (ReadOnlyMemory<byte>)der)], targetHost);
        if (!verdict.IsAccepted)
        {
            throw new CoapException(verdict.ExitCode, verdict.ErrorMessage!);
        }

        return certificate.CertificateList[0];
    }

    /// <summary>Sends ClientKeyExchange, ChangeCipherSpec and the protected Finished, and checks the server's Finished (RFC 5246 section 7.4.9).</summary>
    private async ValueTask<DtlsClientSession> FinishAsync(byte[] masterSecret, Tls12KeyBlock keys, byte[] keyExchangeFragment, CancellationToken cancellationToken)
    {
        DtlsAes128GcmRecordProtector protector = new(keys.ClientWrite, 1);
        DtlsAes128GcmRecordUnprotector unprotector = new(keys.ServerWrite, 1);
        DtlsClientSession session = new(channel, protector, unprotector);
        try
        {
            openRecord = unprotector.TryUnprotect;
            byte[] verifyData = TlsPrf.Sha256.ComputeClientVerifyData(masterSecret, SHA256.HashData([.. transcript]));
            byte[] finishedFragment = new DtlsHandshakeMessage(HandshakeType.Finished, nextMessageSequence++, verifyData).TranscriptBytes();
            transcript.AddRange(finishedFragment);
            List<DtlsHandshakeMessage> flight = await SendFlightAsync(
                [Epoch0Record(TlsContentType.Handshake, keyExchangeFragment), Epoch0Record(TlsContentType.ChangeCipherSpec, [1]), protector.Protect(TlsContentType.Handshake, finishedFragment)],
                type => type == HandshakeType.Finished,
                cancellationToken).ConfigureAwait(false);
            byte[] expected = TlsPrf.Sha256.ComputeServerVerifyData(masterSecret, SHA256.HashData([.. transcript]));
            Require(CryptographicOperations.FixedTimeEquals(flight[^1].Body, expected), "the server's Finished does not match the handshake");
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private async ValueTask<List<DtlsHandshakeMessage>> SendFlightAsync(IReadOnlyList<ReadOnlyMemory<byte>> flight, Func<HandshakeType, bool> endsServerFlight, CancellationToken cancellationToken) =>
        await retransmitter.SendFlightAsync(flight, stop => ReadServerFlightAsync(endsServerFlight, stop), cancellationToken).ConfigureAwait(false);

    /// <summary>Reads datagrams until the server's flight ends with a message <paramref name="endsServerFlight" /> accepts.</summary>
    private async Task<List<DtlsHandshakeMessage>> ReadServerFlightAsync(Func<HandshakeType, bool> endsServerFlight, CancellationToken cancellationToken)
    {
        List<DtlsHandshakeMessage> flight = [];
        while (true)
        {
            DatagramReceived datagram = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            AddHandshakeFragments(buffer.AsSpan(0, datagram.Length));
            while (reader.TryTakeNext(out DtlsHandshakeMessage message))
            {
                flight.Add(message);
                if (endsServerFlight(message.MessageType))
                {
                    return flight;
                }
            }
        }
    }

    private void AddHandshakeFragments(ReadOnlySpan<byte> datagram)
    {
        foreach (byte[] record in DtlsDatagramRecords.Split(datagram))
        {
            if (openRecord(record, out DtlsRecordHeader header, out byte[] plaintext) && header.ContentType == TlsContentType.Handshake)
            {
                DtlsDatagramRecords.HandshakeFragmentsOf(plaintext).ForEach(reader.Add);
            }
        }
    }

    private byte[] Epoch0Record(TlsContentType contentType, byte[] fragment) =>
        [.. new DtlsRecordHeader(contentType, DtlsRecordHeader.Dtls12Version, 0, epochZeroSequenceNumber++, (ushort)fragment.Length).Encode(), .. fragment];
}
