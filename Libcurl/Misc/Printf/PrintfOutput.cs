namespace Libcurl.Misc.Printf;

/// <summary>
/// Where formatted bytes go, as <c>lib/mprintf.c</c>'s <c>stream</c> callback: it takes bytes until it
/// refuses one, after which it is <see cref="Stopped"/> and takes nothing more.
/// </summary>
internal abstract class PrintfOutput
{
    private const int RepeatChunkLength = 256;

    /// <summary>Gets how many bytes it has taken: the count the printf functions answer.</summary>
    public int Count { get; private set; }

    /// <summary>Gets a value indicating whether it has refused a byte, which ends the formatting.</summary>
    public bool Stopped { get; private set; }

    /// <summary>Writes <paramref name="bytes"/>, as many as it takes.</summary>
    /// <param name="bytes">The bytes.</param>
    public void Put(ReadOnlySpan<byte> bytes)
    {
        if (Stopped)
        {
            return;
        }

        int accepted = Accept(bytes);
        Count += accepted;
        Stopped = accepted < bytes.Length;
    }

    /// <summary>Writes one byte.</summary>
    /// <param name="value">The byte.</param>
    public void Put(byte value) => Put(new ReadOnlySpan<byte>(in value));

    /// <summary>Writes <paramref name="value"/> <paramref name="count"/> times; nothing for a count below 1.</summary>
    /// <param name="value">The byte.</param>
    /// <param name="count">How many times.</param>
    public void PutRepeated(byte value, int count)
    {
        Span<byte> chunk = stackalloc byte[RepeatChunkLength];
        chunk.Fill(value);
        for (int left = count; left > 0 && !Stopped; left -= RepeatChunkLength)
        {
            Put(chunk[..Math.Min(left, RepeatChunkLength)]);
        }
    }

    /// <summary>Takes the first bytes of <paramref name="bytes"/> it has room for.</summary>
    /// <param name="bytes">The bytes offered.</param>
    /// <returns>How many it took.</returns>
    protected abstract int Accept(ReadOnlySpan<byte> bytes);
}

/// <summary>An output that keeps up to <c>limit</c> bytes in memory and refuses the rest.</summary>
/// <param name="limit">How many bytes it keeps.</param>
internal sealed class PrintfMemoryOutput(int limit) : PrintfOutput
{
    private readonly MemoryStream written = new();

    /// <summary>Gets the bytes kept.</summary>
    public ReadOnlySpan<byte> Written => written.GetBuffer().AsSpan(0, (int)written.Length);

    /// <inheritdoc/>
    protected override int Accept(ReadOnlySpan<byte> bytes)
    {
        int taken = Math.Min(bytes.Length, limit - (int)written.Length);
        written.Write(bytes[..taken]);
        return taken;
    }
}

/// <summary>An output that writes every byte to a stream, the <c>FILE *</c> of <c>curl_mfprintf</c>.</summary>
/// <param name="stream">The stream.</param>
internal sealed class PrintfStreamOutput(Stream stream) : PrintfOutput
{
    /// <inheritdoc/>
    protected override int Accept(ReadOnlySpan<byte> bytes)
    {
        stream.Write(bytes);
        return bytes.Length;
    }
}
