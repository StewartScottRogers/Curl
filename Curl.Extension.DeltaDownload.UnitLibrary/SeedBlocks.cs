namespace Curl.Extension.DeltaDownload;

/// <summary>
/// The blocks of a zsync target file found in a local seed file (ADR-1090): a window of one block's length is
/// rolled over the seed, zero-padded by one block so the short last block can match, and where its weak checksum
/// equals a block's the window's MD4 digest is compared too; with <c>Hash-Lengths</c> sequence matches of 2 the
/// following block must match right after it as well. A match moves the window on by a whole block.
/// </summary>
internal sealed class SeedBlocks
{
    private readonly ZsyncControlFile control;
    private readonly byte[] seed;
    private readonly Dictionary<uint, List<int>> blocksByWeakChecksum = [];

    private SeedBlocks(ZsyncControlFile control, ReadOnlySpan<byte> seed)
    {
        this.control = control;
        this.seed = new byte[seed.Length + control.BlockSize];
        seed.CopyTo(this.seed);
        Target = new byte[control.BlockCount * control.BlockSize];
        Found = new bool[control.BlockCount];
        for (var block = 0; block < control.BlockCount; block++)
        {
            var weak = control.WeakChecksum(block);
            blocksByWeakChecksum.TryAdd(weak, []);
            blocksByWeakChecksum[weak].Add(block);
        }
    }

    /// <summary>Gets the target file, block by block, holding every block found; its length is whole blocks.</summary>
    public byte[] Target { get; }

    /// <summary>Gets, for each block, whether the seed held it.</summary>
    public bool[] Found { get; }

    /// <summary>Finds every block of <paramref name="control" />'s target in <paramref name="seed" />.</summary>
    public static SeedBlocks Find(ZsyncControlFile control, ReadOnlySpan<byte> seed)
    {
        var blocks = new SeedBlocks(control, seed);
        if (seed.Length > 0)
        {
            blocks.Scan();
        }

        return blocks;
    }

    /// <summary>Gives the byte ranges of the target, first and last inclusive, made of blocks the seed did not hold.</summary>
    public IReadOnlyList<(long First, long Last)> MissingRanges()
    {
        List<(long First, long Last)> ranges = [];
        for (var block = 0; block < Found.Length; block++)
        {
            if (Found[block])
            {
                continue;
            }

            var end = block;
            while (end + 1 < Found.Length && !Found[end + 1])
            {
                end++;
            }

            ranges.Add(((long)block * control.BlockSize, Math.Min((long)(end + 1) * control.BlockSize, control.Length) - 1));
            block = end;
        }

        return ranges;
    }

    private void Scan()
    {
        var size = control.BlockSize;
        var offset = 0;
        var checksum = RollingChecksum.Of(seed.AsSpan(0, size));
        while (true)
        {
            if (MatchAt(offset, checksum))
            {
                offset += size;
                if (offset + size > seed.Length)
                {
                    return;
                }

                checksum = RollingChecksum.Of(seed.AsSpan(offset, size));
                continue;
            }

            if (offset + size == seed.Length)
            {
                return;
            }

            checksum = checksum.Roll(seed[offset], seed[offset + size], size);
            offset++;
        }
    }

    private bool MatchAt(int offset, RollingChecksum checksum)
    {
        if (!blocksByWeakChecksum.TryGetValue(control.Truncate(checksum), out var candidates))
        {
            return false;
        }

        var digest = Md4.Hash(seed.AsSpan(offset, control.BlockSize));
        var matched = false;
        foreach (var block in candidates.Where(block => IsBlock(digest, block) && FollowingBlockMatches(offset, block)))
        {
            Found[block] = true;
            seed.AsSpan(offset, control.BlockSize).CopyTo(Target.AsSpan(block * control.BlockSize));
            matched = true;
        }

        return matched;
    }

    private bool IsBlock(byte[] digest, int block) =>
        digest.AsSpan(0, control.StrongChecksumBytes).SequenceEqual(control.StrongChecksum(block));

    private bool FollowingBlockMatches(int offset, int block)
    {
        var next = offset + control.BlockSize;
        if (control.SequenceMatches == 1 || block + 1 == control.BlockCount)
        {
            return true;
        }

        if (next + control.BlockSize > seed.Length)
        {
            return false;
        }

        var window = seed.AsSpan(next, control.BlockSize);
        return control.Truncate(RollingChecksum.Of(window)) == control.WeakChecksum(block + 1) && IsBlock(Md4.Hash(window), block + 1);
    }
}
