using System.Security.Cryptography;

namespace Curl.Extension.ContentDigest;

/// <summary>
/// A write-only stream that passes every byte of the response body on to the transfer's output unchanged and
/// hashes it with each algorithm the extension checks, SHA-256 and SHA-512 (RFC 9530 §5, "Active").
/// </summary>
/// <param name="output">Where the bytes go on to.</param>
internal sealed class DigestingStream(Stream output) : Stream
{
    private readonly IncrementalHash _sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    private readonly IncrementalHash _sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gives the digest of every byte written so far with the named algorithm.</summary>
    /// <param name="algorithm">The algorithm's name in the Hash Algorithms for HTTP Digest Fields registry.</param>
    /// <returns>The digest, or <see langword="null" /> for an algorithm the extension does not check.</returns>
    internal byte[]? DigestOf(string algorithm) => algorithm switch
    {
        DigestAlgorithms.Sha256 => _sha256.GetCurrentHash(),
        DigestAlgorithms.Sha512 => _sha512.GetCurrentHash(),
        _ => null,
    };

    public override void Write(byte[] buffer, int offset, int count)
    {
        output.Write(buffer, offset, count);
        Append(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        Append(buffer.Span);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => output.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _sha256.Dispose();
        _sha512.Dispose();
        base.Dispose(disposing);
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        _sha256.AppendData(bytes);
        _sha512.AppendData(bytes);
    }
}
