namespace Curl.Tls;

/// <summary>
/// The current read state of the TLS 1.2, 1.1 and 1.0 record layer (RFC 5246 section
/// 6.1): it removes the protection from one record's fragment at a time, with its own
/// sequence number. The caller reads the record header off the transport and checks its
/// version; a failure is the alert to send, never an exception.
/// </summary>
public sealed class Tls12RecordReadState : IDisposable
{
    /// <summary>The longest fragment a protected record may carry (RFC 5246 section 6.2.3).</summary>
    public const int MaximumCiphertextLength = Tls12RecordWriteState.MaximumFragmentLength + 2048;

    private readonly TlsProtocolVersion version;

    private readonly Tls12RecordCipher cipher;

    private Tls12RecordReadState(TlsProtocolVersion version, Tls12RecordCipher cipher)
    {
        this.version = version;
        this.cipher = cipher;
    }

    /// <summary>Gets the sequence number the next record is checked with.</summary>
    public ulong SequenceNumber { get; private set; }

    /// <summary>Creates the initial read state: no encryption and no MAC.</summary>
    /// <param name="version">The version the MAC would cover; unused until a cipher is negotiated.</param>
    /// <returns>The read state.</returns>
    public static Tls12RecordReadState CreatePlaintext(TlsProtocolVersion version) =>
        Create(new Tls12RecordProtectionParameters(version, Tls12BulkCipher.Null, Tls12MacAlgorithm.None), Tls12WriteKeys.None);

    /// <summary>Creates the read state the peer's ChangeCipherSpec switches to.</summary>
    /// <param name="parameters">The negotiated record protection.</param>
    /// <param name="keys">The peer's write keys from <see cref="Tls12KeyBlock" />.</param>
    /// <returns>The read state.</returns>
    /// <exception cref="ArgumentException">The parameters name no suite, or a key has the wrong length.</exception>
    public static Tls12RecordReadState Create(Tls12RecordProtectionParameters parameters, Tls12WriteKeys keys)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(keys);
        parameters.Validate(keys);
        return new Tls12RecordReadState(parameters.Version, Tls12RecordCipher.Create(parameters, keys, null));
    }

    /// <summary>
    /// Checks and removes the protection from one record's fragment (the bytes after its
    /// header). A bad MAC, bad padding, a failed AEAD tag or a malformed length is
    /// <c>bad_record_mac</c>; a fragment or content over the limits is <c>record_overflow</c>.
    /// </summary>
    /// <param name="contentType">The record header's content type.</param>
    /// <param name="fragment">The record's fragment.</param>
    /// <returns>The record's content, or the alert to send.</returns>
    public TlsDecodeResult<byte[]> Unprotect(TlsContentType contentType, ReadOnlySpan<byte> fragment)
    {
        if (fragment.Length > MaximumCiphertextLength)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.RecordOverflow);
        }

        TlsDecodeResult<byte[]> content = cipher.Open(SequenceNumber, contentType, version, fragment);
        SequenceNumber++;
        return content.Succeeded && content.Value.Length > Tls12RecordWriteState.MaximumFragmentLength
            ? TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.RecordOverflow)
            : content;
    }

    /// <summary>Zeroes and releases the keys.</summary>
    public void Dispose() => cipher.Dispose();
}
