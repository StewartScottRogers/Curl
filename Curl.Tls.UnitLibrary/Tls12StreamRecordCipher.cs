using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Stream cipher record protection (RFC 5246 section 6.2.3.1), RC4's: the content and its
/// MAC, encrypted by a keystream that runs on from one record to the next, with no IV and
/// no padding.
/// </summary>
internal sealed class Tls12StreamRecordCipher(Rc4 rc4, Tls12RecordMac mac) : Tls12RecordCipher
{
    public override byte[] Seal(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> content)
    {
        byte[] fragment = new byte[content.Length + mac.Length];
        content.CopyTo(fragment);
        mac.Compute(sequenceNumber, contentType, version, content, fragment.AsSpan(content.Length));
        rc4.ApplyKeyStream(fragment, fragment);
        return fragment;
    }

    public override TlsDecodeResult<byte[]> Open(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, ReadOnlySpan<byte> fragment)
    {
        if (fragment.Length < mac.Length)
        {
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        byte[] plaintext = new byte[fragment.Length];
        rc4.ApplyKeyStream(fragment, plaintext);
        int contentLength = plaintext.Length - mac.Length;
        if (!mac.Verify(sequenceNumber, contentType, version, plaintext.AsSpan(0, contentLength), plaintext.AsSpan(contentLength)))
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return TlsDecodeResult<byte[]>.Failure(TlsAlertDescription.BadRecordMac);
        }

        return TlsDecodeResult<byte[]>.Success(plaintext[..contentLength]);
    }

    public override void Dispose()
    {
        rc4.Dispose();
        mac.Dispose();
    }
}
