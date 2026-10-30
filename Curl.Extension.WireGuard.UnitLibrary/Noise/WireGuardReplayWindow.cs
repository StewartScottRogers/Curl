namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// RFC 6479's sliding window of the transport counters already received (whitepaper section
/// 5.4.6): a ring of 128 64-bit blocks, so the window is 127 x 64 = 8128 counters behind the
/// greatest one seen. A counter is checked before the message is opened and marked only after
/// it has authenticated, so a forged message cannot move the window.
/// </summary>
internal sealed class WireGuardReplayWindow
{
    /// <summary>How many counters below the greatest one seen are still accepted.</summary>
    public const ulong WindowSize = (RingBlocks - 1) * BlockBits;

    private const int BlockBits = 64;

    private const int RingBlocks = 128;

    private readonly ulong[] ring = new ulong[RingBlocks];

    private ulong nextCounter;

    private ulong greatestBlock;

    /// <summary>
    /// Whether <paramref name="counter" /> is below <paramref name="limit" />, inside the window
    /// and not yet marked.
    /// </summary>
    public bool IsAcceptable(ulong counter, ulong limit)
    {
        if (counter >= limit)
        {
            return false;
        }

        if (counter >= nextCounter)
        {
            return true;
        }

        return nextCounter - counter <= WindowSize
            && (ring[(counter / BlockBits) % RingBlocks] & (1UL << (int)(counter % BlockBits))) == 0;
    }

    /// <summary>Marks <paramref name="counter" />, which <see cref="IsAcceptable" /> accepted, as received.</summary>
    public void Mark(ulong counter)
    {
        var block = counter / BlockBits;
        if (counter >= nextCounter)
        {
            var blocksToClear = Math.Min(block - greatestBlock, RingBlocks);
            for (ulong offset = 1; offset <= blocksToClear; offset++)
            {
                ring[(greatestBlock + offset) % RingBlocks] = 0;
            }

            greatestBlock = block;
            nextCounter = counter + 1;
        }

        ring[block % RingBlocks] |= 1UL << (int)(counter % BlockBits);
    }
}
