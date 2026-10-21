using System.Buffers.Binary;
using System.Net.Security;
using Curl.Tls;

namespace Curl.Conformance;

/// <summary>
/// Puts the server side of TLS on an in-memory server stream (such as one end of an
/// <see cref="InMemoryDuplexStream"/> pair), so a TLS stand-in for upstream's stunnel-fronted
/// servers can speak TLS without a socket. It adds no protocol: the caller reads and writes the
/// returned stream as the plain-text server would.
/// </summary>
public static class TlsServerStream
{
    private const int RecordHeaderLength = 5;

    /// <summary>
    /// Gets a value indicating whether this platform's <see cref="SslStream"/> server negotiates
    /// TLS 1.3. On macOS it negotiates at most TLS 1.2, so a client offering only TLS 1.3 (curl
    /// with an <c>--ech</c> config list) could not connect to the stunnel stand-in there as it
    /// connects to upstream's stunnel, which speaks TLS 1.3 (GF-0092).
    /// </summary>
    public static bool PlatformServesTls13 { get; } = !OperatingSystem.IsMacOS();

    /// <summary>Runs the server's TLS handshake on <paramref name="innerStream"/>.</summary>
    /// <param name="innerStream">The server's end of the connection; disposed with the returned stream, or at once if the handshake fails.</param>
    /// <param name="options">The certificate, ALPN protocols and client-certificate request.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The authenticated stream; its <see cref="SslStream.NegotiatedApplicationProtocol"/> and <see cref="SslStream.RemoteCertificate"/> say what the client agreed and sent.</returns>
    public static async Task<SslStream> AuthenticateAsync(Stream innerStream, TlsServerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentNullException.ThrowIfNull(options);
        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);
        try
        {
            await sslStream.AuthenticateAsServerAsync(CreateAuthenticationOptions(options), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            sslStream.Dispose();
            throw;
        }

        return sslStream;
    }

    /// <summary>
    /// Runs the server's TLS handshake on <paramref name="innerStream"/> as
    /// <see cref="AuthenticateAsync"/> does, except where <paramref name="platformServesTls13"/> is
    /// <see langword="false"/> and the client's first record is a ClientHello offering only TLS 1.3,
    /// with no client certificate asked for: that hello is answered by Curl's own TLS 1.3 server
    /// code (<see cref="Tls13ServerStream"/>), as upstream's stunnel answers it with OpenSSL.
    /// </summary>
    /// <param name="innerStream">The server's end of the connection; disposed with the returned stream, or at once if the handshake fails.</param>
    /// <param name="options">The certificate, ALPN protocols and client-certificate request.</param>
    /// <param name="platformServesTls13">Whether <see cref="SslStream"/> serves TLS 1.3 here: <see cref="PlatformServesTls13"/>, or a test's choice.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The authenticated stream; end it with <see cref="ShutdownAsync"/>.</returns>
    public static async Task<Stream> AcceptAsync(Stream innerStream, TlsServerOptions options, bool platformServesTls13, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentNullException.ThrowIfNull(options);
        if (platformServesTls13)
        {
            return await AuthenticateAsync(innerStream, options, cancellationToken).ConfigureAwait(false);
        }

        byte[] firstRecord;
        try
        {
            firstRecord = await ReadFirstRecordAsync(innerStream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            innerStream.Dispose();
            throw;
        }

        byte[]? tls13OnlyHello = FindTls13OnlyClientHello(firstRecord);
        return tls13OnlyHello is null || options.RequestClientCertificate
            ? await AuthenticateAsync(new ReplayedPrefixStream(firstRecord, innerStream), options, cancellationToken).ConfigureAwait(false)
            : await Tls13ServerStream.AuthenticateAsync(innerStream, tls13OnlyHello, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends close_notify on a stream <see cref="AcceptAsync"/> returned, as stunnel does when its server closes.</summary>
    /// <param name="tls">The stream <see cref="AcceptAsync"/> returned.</param>
    /// <returns>A task that completes once the alert is written.</returns>
    public static Task ShutdownAsync(Stream tls) =>
        tls is Tls13ServerStream handBuilt ? handBuilt.ShutdownAsync() : ((SslStream)tls).ShutdownAsync();

    /// <summary>
    /// Returns the ClientHello handshake message <paramref name="firstRecord"/> carries when the
    /// record is a handshake record holding a whole ClientHello whose <c>supported_versions</c>
    /// offers nothing below TLS 1.3; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="firstRecord">The client's first record, header included.</param>
    /// <returns>The ClientHello handshake message, header included, or <see langword="null"/>.</returns>
    internal static byte[]? FindTls13OnlyClientHello(byte[] firstRecord)
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(firstRecord.AsSpan(RecordHeaderLength));
        bool isClientHello = firstRecord[0] == (byte)TlsContentType.Handshake && read.Message?.Type == HandshakeType.ClientHello;
        return isClientHello && OffersOnlyTls13(read.Message!.Body)
            ? firstRecord[RecordHeaderLength..(RecordHeaderLength + read.BytesConsumed)]
            : null;
    }

    private static bool OffersOnlyTls13(byte[] clientHelloBody)
    {
        TlsDecodeResult<ClientHello> hello = ClientHello.Decode(clientHelloBody);
        IReadOnlyList<TlsExtension> extensions = hello.Succeeded ? hello.Value.Extensions : [];
        TlsExtension? versions = extensions.FirstOrDefault(extension => extension.Type == TlsExtensionType.SupportedVersions);
        if (versions is null)
        {
            return false;
        }

        TlsDecodeResult<IReadOnlyList<ushort>> offered = SupportedVersionsExtension.DecodeOffered(versions.Data);
        return offered.Succeeded && offered.Value.All(version => version >= 0x0304);
    }

    /// <summary>Reads the client's first TLS record whole, or only its header when it is not a handshake record.</summary>
    private static async Task<byte[]> ReadFirstRecordAsync(Stream innerStream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[RecordHeaderLength];
        await innerStream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        if (header[0] != (byte)TlsContentType.Handshake)
        {
            return header;
        }

        byte[] record = new byte[RecordHeaderLength + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
        header.CopyTo(record, 0);
        await innerStream.ReadExactlyAsync(record.AsMemory(RecordHeaderLength), cancellationToken).ConfigureAwait(false);
        return record;
    }

    private static SslServerAuthenticationOptions CreateAuthenticationOptions(TlsServerOptions options) => new()
    {
        ServerCertificate = options.Certificate,
        ClientCertificateRequired = options.RequestClientCertificate,
        RemoteCertificateValidationCallback = options.ClientCertificateVerifier is { } verifier
            ? (_, certificate, _, _) => verifier.Verify(certificate)
            : static (_, _, _, _) => true,
        ApplicationProtocols = options.ApplicationProtocols.Count == 0
            ? null
            : [.. options.ApplicationProtocols.Select(protocol => new SslApplicationProtocol(protocol))],
    };
}
