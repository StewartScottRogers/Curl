using System.Numerics;

namespace Curl.Cryptography;

/// <summary>
/// Keccak-p[1600, 24], the permutation under SHA-3 and SHAKE (FIPS 202 section 3.3): 24
/// rounds of theta, rho, pi, chi and iota on 25 lanes of 64 bits, lane (x, y) at index
/// x + 5y. Constant-time: every step is the same shifts, XORs and ANDs whatever the lanes
/// hold.
/// </summary>
internal static class KeccakPermutation
{
    /// <summary>The number of 64-bit lanes in the state.</summary>
    public const int LaneCount = 25;

    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000,
        0x000000000000808B, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
        0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
        0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003,
        0x8000000000008002, 0x8000000000000080, 0x000000000000800A, 0x800000008000000A,
        0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008,
    ];

    // Rho's rotation offsets and pi's destination lanes, in the order the lane at index 1
    // travels round the cycle pi makes of the 24 lanes other than (0, 0).
    private static readonly int[] RotationOffsets =
        [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14, 27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44];

    private static readonly int[] PiLanes =
        [10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4, 15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1];

    /// <summary>Applies the 24 rounds to <paramref name="state" /> in place.</summary>
    public static void Permute(Span<ulong> state)
    {
        Span<ulong> columns = stackalloc ulong[5];
        foreach (ulong roundConstant in RoundConstants)
        {
            Theta(state, columns);
            RhoAndPi(state);
            Chi(state, columns);
            state[0] ^= roundConstant;
        }
    }

    private static void Theta(Span<ulong> state, Span<ulong> columns)
    {
        for (int x = 0; x < 5; x++)
        {
            columns[x] = state[x] ^ state[x + 5] ^ state[x + 10] ^ state[x + 15] ^ state[x + 20];
        }

        for (int x = 0; x < 5; x++)
        {
            ulong difference = columns[(x + 4) % 5] ^ BitOperations.RotateLeft(columns[(x + 1) % 5], 1);
            for (int y = 0; y < LaneCount; y += 5)
            {
                state[y + x] ^= difference;
            }
        }
    }

    private static void RhoAndPi(Span<ulong> state)
    {
        ulong carried = state[1];
        for (int step = 0; step < PiLanes.Length; step++)
        {
            int lane = PiLanes[step];
            ulong displaced = state[lane];
            state[lane] = BitOperations.RotateLeft(carried, RotationOffsets[step]);
            carried = displaced;
        }
    }

    private static void Chi(Span<ulong> state, Span<ulong> row)
    {
        for (int y = 0; y < LaneCount; y += 5)
        {
            state.Slice(y, 5).CopyTo(row);
            for (int x = 0; x < 5; x++)
            {
                state[y + x] = row[x] ^ (~row[(x + 1) % 5] & row[(x + 2) % 5]);
            }
        }
    }
}
