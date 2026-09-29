using System.Buffers.Binary;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.2, 1.1 and 1.0 record layer over a caller's byte stream (RFC 5246 section
/// 6): it reads whole records off the transport and removes their protection with the
/// current <see cref="Tls12RecordReadState" />, and writes content under the current
/// <see cref="Tls12RecordWriteState" />; the connection switches each state at its
/// ChangeCipherSpec. Records before the version is fixed carry TLS 1.0, as OpenSSL's
/// ClientHello record does; after it, every record read must carry the negotiated
/// version. Writes are serialised, so an alert sent from a read cannot interleave with an
/// application write.
/// </summary>
internal sealed class Tls12RecordLayer(Stream transport) : IDisposable
{
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private Tls12RecordReadState reader = Tls12RecordReadState.CreatePlaintext(TlsProtocolVersion.Tls10);
    private Tls12RecordWriteState writer = Tls12RecordWriteState.CreatePlaintext(TlsProtocolVersion.Tls10);
    private TlsProtocolVersion? version;

    /// <summary>
    /// Fixes the version the server chose, once: plaintext records from here on carry it,
    /// and records read must carry it.
    /// </summary>
    public void FixVersion(TlsProtocolVersion negotiated)
    {
        if (version is null)
        {
            version = negotiated;
            SwitchWriteState(Tls12RecordWriteState.CreatePlaintext(negotiated));
        }
    }

    /// <summary>Puts a new read state in force, after the server's ChangeCipherSpec.</summary>
    public void SwitchReadState(Tls12RecordReadState state)
    {
        reader.Dispose();
        reader = state;
    }

    /// <summary>Puts a new write state in force, after the client's ChangeCipherSpec.</summary>
    public void SwitchWriteState(Tls12RecordWriteState state)
    {
        writer.Dispose();
        writer = state;
    }

    /// <summary>
    /// Reads one whole record and removes its protection. Returns <see langword="null" />
    /// when the transport ends, at a record boundary or inside a record.
    /// </summary>
    /// <exception cref="TlsAlertException">
    /// The record carries the wrong version (<c>protocol_version</c>), or fails its checks
    /// (<c>bad_record_mac</c>, <c>record_overflow</c>); the exception carries the alert to send.
    /// </exception>
    public async Task<Tls12RecordContent?> ReceiveAsync(CancellationToken cancellationToken)
    {
        byte[] header = new byte[Tls12RecordWriteState.RecordHeaderLength];
        if (await transport.ReadAtLeastAsync(header, header.Length, false, cancellationToken).ConfigureAwait(false) < header.Length)
        {
            return null;
        }

        CheckVersion(BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1)));
        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(3));
        byte[] fragment = new byte[length];
        if (await transport.ReadAtLeastAsync(fragment, length, false, cancellationToken).ConfigureAwait(false) < length)
        {
            return null;
        }

        TlsContentType type = (TlsContentType)header[0];
        TlsDecodeResult<byte[]> content = reader.Unprotect(type, fragment);
        return content.Succeeded ? new Tls12RecordContent(type, content.Value) : throw new TlsAlertException(content.Alert!.Value, false);
    }

    /// <summary>Protects and sends <paramref name="content" /> as records of <paramref name="contentType" />.</summary>
    public async Task SendAsync(TlsContentType contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await transport.WriteAsync(writer.Protect(contentType, content.Span), cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    /// Sends <paramref name="alert" /> under the current write state: <c>close_notify</c> as a
    /// warning, every other alert as fatal. A transport that fails meanwhile is ignored,
    /// since the alert is the last thing sent.
    /// </summary>
    public async Task SendAlertAsync(TlsAlertDescription alert, CancellationToken cancellationToken)
    {
        byte level = alert == TlsAlertDescription.CloseNotify ? (byte)1 : (byte)2;
        try
        {
            await SendAsync(TlsContentType.Alert, new byte[] { level, (byte)alert }, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The alert is best effort: the connection is failing or closing either way.
        }
    }

    /// <summary>Flushes the transport.</summary>
    public Task FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <summary>Zeroes the keys; the transport stays open for its owner.</summary>
    public void Dispose()
    {
        reader.Dispose();
        writer.Dispose();
        writeLock.Dispose();
    }

    // Before the ServerHello any TLS version is accepted; OpenSSL answers a record of
    // another major version ("wrong version number") and, after it, one of another version
    // with protocol_version.
    private void CheckVersion(ushort recordVersion)
    {
        bool fits = version is { } negotiated ? recordVersion == (ushort)negotiated : recordVersion >> 8 == 3;
        if (!fits)
        {
            throw new TlsAlertException(TlsAlertDescription.ProtocolVersion, false);
        }
    }
}
