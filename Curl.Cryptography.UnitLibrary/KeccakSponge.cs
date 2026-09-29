using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The sponge construction on Keccak-p[1600, 24] (FIPS 202 section 4) with the byte-aligned
/// pad10*1 of section B.2: the domain bits and the first padding bit are one byte
/// (<c>0x06</c> for SHA-3, <c>0x1F</c> for SHAKE) and the last padding bit is <c>0x80</c>
/// in the rate's last byte. Absorbs any number of pieces, then squeezes any number of
/// pieces; absorbing after the first squeeze is refused. Constant-time in the bytes; only
/// their count shapes the running time. <see cref="Dispose" /> zeroes the state.
/// </summary>
internal sealed class KeccakSponge : IDisposable
{
    private const byte FinalPaddingBit = 0x80;

    private readonly ulong[] state = new ulong[KeccakPermutation.LaneCount];
    private readonly int rate;
    private readonly byte domainPadding;
    private int position;
    private bool squeezing;

    /// <summary>
    /// Creates an empty sponge with a rate of <paramref name="rate" /> bytes (200 minus twice
    /// the security strength in bytes) and the domain-and-padding byte
    /// <paramref name="domainPadding" />.
    /// </summary>
    public KeccakSponge(int rate, byte domainPadding)
    {
        this.rate = rate;
        this.domainPadding = domainPadding;
    }

    /// <summary>Absorbs <paramref name="data" /> after everything absorbed before it.</summary>
    /// <exception cref="InvalidOperationException">Output has already been squeezed.</exception>
    public void Absorb(ReadOnlySpan<byte> data)
    {
        if (squeezing)
        {
            throw new InvalidOperationException("The sponge has already been squeezed; reset it before absorbing again.");
        }

        foreach (byte value in data)
        {
            XorByte(position, value);
            position++;
            if (position == rate)
            {
                KeccakPermutation.Permute(state);
                position = 0;
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="output" /> with the next bytes of the output, padding and
    /// switching to squeezing on the first call.
    /// </summary>
    public void Squeeze(Span<byte> output)
    {
        if (!squeezing)
        {
            XorByte(position, domainPadding);
            XorByte(rate - 1, FinalPaddingBit);
            KeccakPermutation.Permute(state);
            position = 0;
            squeezing = true;
        }

        for (int index = 0; index < output.Length; index++)
        {
            if (position == rate)
            {
                KeccakPermutation.Permute(state);
                position = 0;
            }

            output[index] = (byte)(state[position >> 3] >> (8 * (position & 7)));
            position++;
        }
    }

    /// <summary>Returns the sponge to empty, absorbing, with the state zeroed.</summary>
    public void Reset()
    {
        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(state.AsSpan()));
        position = 0;
        squeezing = false;
    }

    /// <summary>Zeroes the state.</summary>
    public void Dispose() => Reset();

    private void XorByte(int index, byte value) => state[index >> 3] ^= (ulong)value << (8 * (index & 7));
}
