using System.Buffers.Binary;
using System.Security.Authentication;
using Curl.Tls;

namespace Curl.Conformance;

/// <summary>
/// The server side of a TLS 1.3 connection built from Curl's own TLS code, for a platform whose
/// <see cref="System.Net.Security.SslStream"/> server cannot negotiate TLS 1.3 (macOS):
/// <see cref="AuthenticateAsync"/> answers the client's ClientHello as
/// <see cref="Tls13ServerHandshake"/> does and checks the client's Finished, then the stream
/// carries application data in records protected under the application traffic secrets. Any
/// record other than application data - close_notify or another alert - ends what it reads.
/// </summary>
internal sealed class Tls13ServerStream : Stream
{
    private const int RecordHeaderLength = Tls13RecordProtection.RecordHeaderLength;

    private readonly Stream transport;

    private readonly Tls13RecordProtection reader;

    private readonly Tls13RecordProtection writer;

    private byte[] pending = [];

    private int pendingOffset;

    private Tls13ServerStream(Stream transport, Tls13ServerHandshake handshake)
    {
        this.transport = transport;
        reader = Tls13RecordProtection.Create(Tls13ServerHandshake.Suite, handshake.ClientApplicationTrafficSecret);
        writer = Tls13RecordProtection.Create(Tls13ServerHandshake.Suite, handshake.ServerApplicationTrafficSecret);
        NegotiatedApplicationProtocol = handshake.ApplicationProtocol;
    }

    /// <summary>Gets the ALPN protocol the handshake chose, or <see langword="null"/> when it chose none.</summary>
    public string? NegotiatedApplicationProtocol { get; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("A TLS stream has no length.");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException("A TLS stream has no position.");
        set => throw new NotSupportedException("A TLS stream has no position.");
    }

    /// <summary>Runs the server's TLS 1.3 handshake on <paramref name="transport"/>, whose ClientHello has already been read.</summary>
    /// <param name="transport">The server's end of the connection; owned by the returned stream, and disposed at once if the handshake fails.</param>
    /// <param name="clientHelloMessage">The ClientHello handshake message, header included.</param>
    /// <param name="options">The certificate and the ALPN protocols offered.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The connected stream.</returns>
    /// <exception cref="AuthenticationException">The handshake failed: see <see cref="Tls13ServerHandshake.Answer"/> and <see cref="Tls13ServerHandshake.CheckClientFinished"/>.</exception>
    public static async Task<Tls13ServerStream> AuthenticateAsync(Stream transport, byte[] clientHelloMessage, TlsServerOptions options, CancellationToken cancellationToken)
    {
        try
        {
            Tls13ServerHandshake handshake = Tls13ServerHandshake.Answer(clientHelloMessage, options);
            await transport.WriteAsync(PlaintextHandshakeRecord(handshake.ServerHello), cancellationToken).ConfigureAwait(false);
            using (Tls13RecordProtection handshakeWriter = Tls13RecordProtection.Create(Tls13ServerHandshake.Suite, handshake.ServerHandshakeTrafficSecret))
            {
                await transport.WriteAsync(handshakeWriter.Protect(TlsContentType.Handshake, handshake.EncryptedFlight), cancellationToken).ConfigureAwait(false);
            }

            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
            using Tls13RecordProtection handshakeReader = Tls13RecordProtection.Create(Tls13ServerHandshake.Suite, handshake.ClientHandshakeTrafficSecret);
            Tls13RecordContent clientFinished = await ReadRecordAsync(transport, handshakeReader, cancellationToken).ConfigureAwait(false)
                ?? throw new AuthenticationException("The client closed the connection before its Finished.");
            handshake.CheckClientFinished(clientFinished);
            return new Tls13ServerStream(transport, handshake);
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (pendingOffset == pending.Length)
        {
            Tls13RecordContent? content = await ReadRecordAsync(transport, reader, cancellationToken).ConfigureAwait(false);
            if (content is not { Type: TlsContentType.ApplicationData })
            {
                return 0;
            }

            pending = content.Content;
            pendingOffset = 0;
        }

        int count = Math.Min(buffer.Length, pending.Length - pendingOffset);
        pending.AsMemory(pendingOffset, count).CopyTo(buffer);
        pendingOffset += count;
        return count;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        for (int offset = 0; offset < buffer.Length; offset += Tls13RecordProtection.MaximumPlaintextLength)
        {
            int length = Math.Min(Tls13RecordProtection.MaximumPlaintextLength, buffer.Length - offset);
            await transport.WriteAsync(writer.Protect(TlsContentType.ApplicationData, buffer.Slice(offset, length).Span), cancellationToken).ConfigureAwait(false);
        }

        await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Sends close_notify, as <see cref="System.Net.Security.SslStream.ShutdownAsync"/> does.</summary>
    /// <returns>A task that completes once the alert is written.</returns>
    public async Task ShutdownAsync()
    {
        await transport.WriteAsync(writer.Protect(TlsContentType.Alert, [1, (byte)TlsAlertDescription.CloseNotify])).ConfigureAwait(false);
        await transport.FlushAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override void Flush() => transport.Flush();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Read a TLS stream with ReadAsync.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write a TLS stream with WriteAsync.");

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("A TLS stream cannot seek.");

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("A TLS stream has no length.");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            transport.Dispose();
            reader.Dispose();
            writer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static byte[] PlaintextHandshakeRecord(byte[] message)
    {
        byte[] record = new byte[RecordHeaderLength + message.Length];
        record[0] = (byte)TlsContentType.Handshake;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(1), Tls13RecordProtection.LegacyRecordVersion);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(3), (ushort)message.Length);
        message.CopyTo(record, RecordHeaderLength);
        return record;
    }

    /// <summary>Reads the client's next record other than change_cipher_spec and unprotects it; <see langword="null"/> once the client has closed the connection.</summary>
    private static async Task<Tls13RecordContent?> ReadRecordAsync(Stream transport, Tls13RecordProtection protection, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] header = new byte[RecordHeaderLength];
            if (await transport.ReadAtLeastAsync(header, RecordHeaderLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false) < RecordHeaderLength)
            {
                return null;
            }

            byte[] record = new byte[RecordHeaderLength + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3))];
            header.CopyTo(record, 0);
            await transport.ReadExactlyAsync(record.AsMemory(RecordHeaderLength), cancellationToken).ConfigureAwait(false);
            if (record[0] != (byte)TlsContentType.ChangeCipherSpec)
            {
                TlsDecodeResult<Tls13RecordContent> content = protection.Unprotect(record);
                return content.Succeeded ? content.Value : throw new IOException($"A record from the client does not open: {content.Alert}.");
            }
        }
    }
}
