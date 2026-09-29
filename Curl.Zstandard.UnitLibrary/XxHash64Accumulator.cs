namespace Curl.Zstandard;

/// <summary>
/// XXH64 over input that arrives in pieces: <see cref="Append" /> any number of times, then
/// <see cref="GetCurrentHash" /> gives what <see cref="XxHash64.Hash" /> gives for all of
/// the input at once. <see cref="ZstandardDecoder" /> feeds it each piece of a frame's
/// content as it writes it.
/// </summary>
public sealed class XxHash64Accumulator
{
    private readonly ulong seed;

    private readonly byte[] pendingStripe = new byte[XxHash64.StripeLength];

    private XxHash64Lanes lanes;

    private int pendingLength;

    private ulong totalLength;

    /// <summary>Starts an empty hash under <paramref name="seed" />.</summary>
    public XxHash64Accumulator(ulong seed = 0)
    {
        this.seed = seed;
        Reset();
    }

    /// <summary>Forgets all input appended so far, keeping the seed.</summary>
    public void Reset()
    {
        lanes = new XxHash64Lanes(seed);
        pendingLength = 0;
        totalLength = 0;
    }

    /// <summary>Appends <paramref name="data" /> to the input hashed so far.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        totalLength += (ulong)data.Length;
        while (!data.IsEmpty)
        {
            var count = Math.Min(XxHash64.StripeLength - pendingLength, data.Length);
            data[..count].CopyTo(pendingStripe.AsSpan(pendingLength));
            pendingLength += count;
            data = data[count..];
            if (pendingLength == XxHash64.StripeLength)
            {
                lanes.ConsumeStripe(pendingStripe);
                pendingLength = 0;
            }
        }
    }

    /// <summary>Returns the XXH64 of everything appended since construction or <see cref="Reset" />.</summary>
    public ulong GetCurrentHash()
    {
        var accumulator = totalLength >= XxHash64.StripeLength ? lanes.Converge() : seed + XxHash64.Prime5;
        return XxHash64.Finish(accumulator, totalLength, pendingStripe.AsSpan(0, pendingLength));
    }
}
