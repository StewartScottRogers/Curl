namespace Curl.Extension.DeltaDownload;

/// <summary>
/// zsync 0.6's weak block checksum, the rsync rolling checksum: <c>a</c> is the sum of a block's bytes and
/// <c>b</c> the sum of each byte times its distance from the block's end, both modulo 2^16, so a window
/// moved one byte along is updated without reading the block again.
/// </summary>
/// <param name="A">The sum of the bytes, modulo 2^16.</param>
/// <param name="B">The weighted sum of the bytes, modulo 2^16.</param>
internal readonly record struct RollingChecksum(ushort A, ushort B)
{
    /// <summary>Gets the checksum as zsync writes it: <c>a</c> then <c>b</c>, each big-endian.</summary>
    public uint Value => ((uint)A << 16) | B;

    /// <summary>Gives the checksum of <paramref name="block" />.</summary>
    public static RollingChecksum Of(ReadOnlySpan<byte> block)
    {
        uint a = 0;
        uint b = 0;
        for (var index = 0; index < block.Length; index++)
        {
            a += block[index];
            b += (uint)(block.Length - index) * block[index];
        }

        return new RollingChecksum((ushort)a, (ushort)b);
    }

    /// <summary>Gives the checksum of the window <paramref name="blockSize" /> bytes long moved one byte along.</summary>
    /// <param name="leaving">The byte that leaves the window at its start.</param>
    /// <param name="entering">The byte that enters the window at its end.</param>
    /// <param name="blockSize">The window's length.</param>
    public RollingChecksum Roll(byte leaving, byte entering, int blockSize)
    {
        var a = (ushort)(A - leaving + entering);
        return new RollingChecksum(a, (ushort)(B - blockSize * leaving + a));
    }
}
