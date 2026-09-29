using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.3 record layer over a caller's byte stream (RFC 8446 section 5): it reads
/// whole records off the transport, holds the read protection and one write protection
/// per encryption level as the handshake installs them, writes plaintext and protected
/// records, and rolls the application keys on KeyUpdate. Writes are serialised, so a
/// KeyUpdate answered from a read cannot interleave with an application write.
/// </summary>
internal sealed class Tls13RecordLayer(Stream transport, Tls13ClientHandshake handshake) : IDisposable
{
    private readonly Dictionary<TlsEncryptionLevel, Tls13RecordProtection> writers = [];
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private Tls13RecordProtection? reader;
    private byte[] readSecret = [];

    /// <summary>Gets the level of the read protection in force: <see cref="TlsEncryptionLevel.Initial" /> while records arrive in plaintext.</summary>
    public TlsEncryptionLevel ReadLevel { get; private set; } = TlsEncryptionLevel.Initial;

    /// <summary>Gets the highest level the client can write at; alerts go out at this level.</summary>
    public TlsEncryptionLevel WriteLevel { get; private set; } = TlsEncryptionLevel.Initial;

    /// <summary>Gets a value indicating whether records from the server are protected.</summary>
    public bool IsReadProtected => reader is not null;

    /// <summary>Installs a traffic secret the handshake derived for one level and direction; early data is protected with the resumed session's suite.</summary>
    public void Install(Tls13TrafficSecret secret)
    {
        Tls13CipherSuite suite = secret.Level == TlsEncryptionLevel.EarlyData ? handshake.EarlyDataCipherSuite! : handshake.CipherSuite!;
        Tls13RecordProtection protection = Tls13RecordProtection.Create(suite, secret.Secret);
        if (secret.Direction == TlsTrafficDirection.Read)
        {
            reader?.Dispose();
            reader = protection;
            readSecret = secret.Secret;
            ReadLevel = secret.Level;
            return;
        }

        writers[secret.Level] = protection;
        WriteLevel = secret.Level;
    }

    /// <summary>
    /// Reads one whole record, header included. Returns <see langword="null" /> when the
    /// transport ends, at a record boundary or inside a record; a header announcing more
    /// than a protected record may hold is <c>record_overflow</c>.
    /// </summary>
    /// <exception cref="TlsAlertException">The record is too long.</exception>
    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        byte[] header = new byte[Tls13RecordProtection.RecordHeaderLength];
        if (await transport.ReadAtLeastAsync(header, header.Length, false, cancellationToken).ConfigureAwait(false) < header.Length)
        {
            return null;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3));
        if (length > Tls13RecordProtection.MaximumCiphertextLength)
        {
            throw new TlsAlertException(TlsAlertDescription.RecordOverflow, false);
        }

        byte[] record = new byte[header.Length + length];
        header.CopyTo(record, 0);
        int read = await transport.ReadAtLeastAsync(record.AsMemory(header.Length), length, false, cancellationToken).ConfigureAwait(false);
        return read < length ? null : record;
    }

    /// <summary>Removes the read protection from a protected record.</summary>
    /// <exception cref="TlsAlertException">The record fails its checks; the exception carries the alert to send.</exception>
    public Tls13RecordContent Open(byte[] record)
    {
        TlsDecodeResult<Tls13RecordContent> content = reader!.Unprotect(record);
        return content.Succeeded ? content.Value : throw new TlsAlertException(content.Alert!.Value, false);
    }

    /// <summary>Sends <paramref name="content" /> at <paramref name="level" />: in plaintext records at the Initial level, protected above it.</summary>
    /// <param name="level">The encryption level.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="content">The content.</param>
    /// <param name="plaintextRecordVersion">The <c>legacy_record_version</c> of plaintext records.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SendAsync(
        TlsEncryptionLevel level,
        TlsContentType contentType,
        ReadOnlyMemory<byte> content,
        ushort plaintextRecordVersion,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] records = level == TlsEncryptionLevel.Initial
                ? PlaintextRecords(contentType, content.Span, plaintextRecordVersion)
                : writers[level].Protect(contentType, content.Span);
            await WriteAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    /// Sends <paramref name="alert" /> at <see cref="WriteLevel" />: <c>close_notify</c> as a
    /// warning, every other alert as fatal. A transport that fails meanwhile is ignored,
    /// since the alert is the last thing sent.
    /// </summary>
    public async Task SendAlertAsync(TlsAlertDescription alert, CancellationToken cancellationToken)
    {
        byte level = alert == TlsAlertDescription.CloseNotify ? (byte)1 : (byte)2;
        try
        {
            await SendAsync(WriteLevel, TlsContentType.Alert, new byte[] { level, (byte)alert }, Tls13RecordProtection.LegacyRecordVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The alert is best effort: the connection is failing or closing either way.
        }
    }

    /// <summary>Moves the read protection to the next application traffic secret (RFC 8446 section 7.2).</summary>
    public void UpdateReadKeys() =>
        Install(new Tls13TrafficSecret(TlsEncryptionLevel.Application, TlsTrafficDirection.Read, handshake.CipherSuite!.KeySchedule.DeriveNextApplicationTrafficSecret(readSecret)));

    /// <summary>
    /// Sends a KeyUpdate under the current application write keys, then moves them to the
    /// next application traffic secret, with no other write in between.
    /// </summary>
    /// <param name="requestUpdate">Whether to ask the server to update its keys too (<c>update_requested</c>).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SendKeyUpdateAsync(bool requestUpdate, CancellationToken cancellationToken)
    {
        byte[] keyUpdate = new HandshakeMessage(HandshakeType.KeyUpdate, [requestUpdate ? (byte)1 : (byte)0]).Encode();
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(writers[TlsEncryptionLevel.Application].Protect(TlsContentType.Handshake, keyUpdate), cancellationToken).ConfigureAwait(false);
            byte[] next = handshake.AdvanceClientApplicationTrafficSecret();
            writers[TlsEncryptionLevel.Application].Dispose();
            Install(new Tls13TrafficSecret(TlsEncryptionLevel.Application, TlsTrafficDirection.Write, next));
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    /// Passes a post-handshake CertificateRequest to the handshake and sends its answer
    /// (Certificate, CertificateVerify and Finished, RFC 8446 section 4.6.2) under the
    /// application write keys, with no other write in between, so a KeyUpdate cannot move
    /// the keys the Finished was keyed from before it goes out.
    /// </summary>
    /// <param name="message">The whole CertificateRequest, header included.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The alert the request calls for, or <see langword="null" /> when it was answered.</returns>
    public async Task<TlsAlertDescription?> AnswerCertificateRequestAsync(byte[] message, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Tls13HandshakeOutput output = handshake.Receive(TlsEncryptionLevel.Application, message);
            foreach (TlsHandshakeBytes answer in output.BytesToSend)
            {
                await WriteAsync(writers[TlsEncryptionLevel.Application].Protect(TlsContentType.Handshake, answer.Bytes), cancellationToken).ConfigureAwait(false);
            }

            return output.Failure?.Alert;
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>Flushes the transport.</summary>
    public Task FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <summary>Zeroes the keys; the transport stays open for its owner.</summary>
    public void Dispose()
    {
        reader?.Dispose();
        foreach (Tls13RecordProtection writer in writers.Values)
        {
            writer.Dispose();
        }

        writeLock.Dispose();
    }

    private static byte[] PlaintextRecords(TlsContentType contentType, ReadOnlySpan<byte> content, ushort recordVersion)
    {
        List<byte> records = [];
        int offset = 0;
        do
        {
            int length = Math.Min(Tls13RecordProtection.MaximumPlaintextLength, content.Length - offset);
            records.AddRange([(byte)contentType, (byte)(recordVersion >> 8), (byte)recordVersion, (byte)(length >> 8), (byte)length]);
            records.AddRange(content.Slice(offset, length));
            offset += length;
        }
        while (offset < content.Length);

        return [.. records];
    }

    private async Task WriteAsync(byte[] records, CancellationToken cancellationToken)
    {
        await transport.WriteAsync(records, cancellationToken).ConfigureAwait(false);
        await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
