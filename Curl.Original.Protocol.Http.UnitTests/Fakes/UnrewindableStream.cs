namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A body stream that cannot seek and that curl could not rewind either
/// (<see cref="IRewindableUpload.CanRewind" /> is <see langword="false" />), as standard input
/// read through a pipe off Windows is (BL-2337); with <see cref="RewindsUntilRead" />, only once
/// it has been read, as curl needs no rewind of a reader it has not read from (BL-5910).
/// </summary>
/// <param name="content">The bytes it reads.</param>
public sealed class UnrewindableStream(byte[] content) : MemoryStream(content), IRewindableUpload
{
    /// <summary>Gets a value indicating whether <see cref="CanRewind" /> is true until the first read.</summary>
    public bool RewindsUntilRead { get; init; }

    /// <summary>Gets a value indicating whether any read has been made.</summary>
    public bool HasBeenRead { get; private set; }

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public bool CanRewind => RewindsUntilRead && !HasBeenRead;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        HasBeenRead = true;
        return base.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        HasBeenRead = true;
        return base.Read(buffer);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        HasBeenRead = true;
        return base.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        HasBeenRead = true;
        return base.ReadAsync(buffer, offset, count, cancellationToken);
    }
}
