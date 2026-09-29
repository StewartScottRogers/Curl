namespace Curl.Tls;

/// <summary>
/// The null cipher: the content as it is, followed by its MAC when the suite has one
/// (the <c>_WITH_NULL_</c> suites), or alone in the initial state.
/// </summary>
internal sealed class Tls12NullRecordCipher(Tls12RecordMac? mac) : Tls12RecordCipher
{
    public override byte[] Seal(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content)
    {
        if (mac is null)
        {
            return content.ToArray();
        }

        byte[] fragment = new byte[content.Length + mac.Length];
        content.CopyTo(fragment);
        mac.Compute(sequenceNumber, contentType, version, content, fragment.AsSpan(content.Length));
        return fragment;
    }

    public override TlsDecodeResult<byte[]> Open(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment)
    {
        if (mac is null)
        {
            return TlsDecodeResult<byte[]>.Success(fragment.ToArray());
        }

        if (fragment.Length < mac.Length)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        ReadOnlySpan<byte> content = fragment[..^mac.Length];
        return mac.Verify(sequenceNumber, contentType, version, content, fragment[^mac.Length..])
            ? TlsDecodeResult<byte[]>.Success(content.ToArray())
            : TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
    }

    public override void Dispose() => mac?.Dispose();
}
