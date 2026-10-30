using System.Globalization;
using System.Text;

namespace Curl.Extension.DeltaDownload;

/// <summary>
/// A zsync 0.6 control file (<c>.zsync</c>): header lines <c>Name: value</c> ending at an empty line, then for each
/// block of the target file its weak checksum's last <see cref="WeakChecksumBytes" /> bytes and its MD4 digest's
/// first <see cref="StrongChecksumBytes" /> bytes (ADR-1092).
/// </summary>
/// <param name="BlockSize">The <c>Blocksize</c> header: the length of every block, the last zero-padded.</param>
/// <param name="Length">The <c>Length</c> header: the target file's length in bytes.</param>
/// <param name="SequenceMatches">The first <c>Hash-Lengths</c> value: how many consecutive blocks must match, 1 or 2.</param>
/// <param name="WeakChecksumBytes">The second <c>Hash-Lengths</c> value: the weak checksum bytes kept, 1 to 4.</param>
/// <param name="StrongChecksumBytes">The third <c>Hash-Lengths</c> value: the MD4 bytes kept, 3 to 16.</param>
/// <param name="Sha1">The <c>SHA-1</c> header: the whole target file's SHA-1 digest.</param>
/// <param name="FileName">The <c>Filename</c> header, or <see langword="null" /> when it has none.</param>
/// <param name="Checksums">The block checksums, each weak bytes then strong bytes.</param>
internal sealed record ZsyncControlFile(int BlockSize, int Length, int SequenceMatches, int WeakChecksumBytes, int StrongChecksumBytes, byte[] Sha1, string? FileName, byte[] Checksums)
{
    /// <summary>Gets the number of blocks the target file is split into.</summary>
    public int BlockCount => (int)(((long)Length + BlockSize - 1) / BlockSize);

    /// <summary>Gets the weak checksum of block <paramref name="block" />, its dropped leading bytes zero.</summary>
    public uint WeakChecksum(int block)
    {
        uint value = 0;
        foreach (var part in Checksums.AsSpan(block * (WeakChecksumBytes + StrongChecksumBytes), WeakChecksumBytes))
        {
            value = (value << 8) | part;
        }

        return value;
    }

    /// <summary>Gets the kept leading bytes of block <paramref name="block" />'s MD4 digest.</summary>
    public ReadOnlySpan<byte> StrongChecksum(int block) =>
        Checksums.AsSpan(block * (WeakChecksumBytes + StrongChecksumBytes) + WeakChecksumBytes, StrongChecksumBytes);

    /// <summary>Gives the weak checksum <paramref name="checksum" /> as the control file keeps it.</summary>
    public uint Truncate(RollingChecksum checksum) =>
        WeakChecksumBytes == 4 ? checksum.Value : checksum.Value & ((1u << (8 * WeakChecksumBytes)) - 1);

    /// <summary>Reads a control file, or gives <see langword="null" /> when <paramref name="bytes" /> is not one.</summary>
    public static ZsyncControlFile? Parse(byte[] bytes)
    {
        var end = bytes.AsSpan().IndexOf("\n\n"u8);
        if (end < 0)
        {
            return null;
        }

        var headers = Headers(Encoding.Latin1.GetString(bytes, 0, end));
        return headers.ContainsKey("zsync") ? FromHeaders(headers, bytes[(end + 2)..]) : null;
    }

    private static Dictionary<string, string> Headers(string text)
    {
        Dictionary<string, string> headers = new(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0)
            {
                headers.TryAdd(line[..colon], line[(colon + 2)..]);
            }
        }

        return headers;
    }

    private static ZsyncControlFile? FromHeaders(Dictionary<string, string> headers, byte[] checksums)
    {
        // Hash-Lengths defaults to 1,4,16, what zsync wrote before it had the header.
        var lengths = headers.GetValueOrDefault("Hash-Lengths", "1,4,16").Split(',');
        if (lengths.Length != 3 || headers.GetValueOrDefault("SHA-1") is not { Length: 40 } sha1 || !IsHex(sha1))
        {
            return null;
        }

        var file = new ZsyncControlFile(
            Number(headers.GetValueOrDefault("Blocksize")),
            Number(headers.GetValueOrDefault("Length")),
            Number(lengths[0]),
            Number(lengths[1]),
            Number(lengths[2]),
            Convert.FromHexString(sha1),
            headers.GetValueOrDefault("Filename"),
            checksums);
        return file.IsConsistent ? file : null;
    }

    private bool IsConsistent => HasHashLengthsInRange && BlockSize > 0 && Length >= 0
        && Checksums.LongLength == (long)BlockCount * (WeakChecksumBytes + StrongChecksumBytes);

    private bool HasHashLengthsInRange =>
        SequenceMatches is 1 or 2 && WeakChecksumBytes is >= 1 and <= 4 && StrongChecksumBytes is >= 3 and <= 16;

    private static bool IsHex(string text) => text.All(char.IsAsciiHexDigit);

    private static int Number(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
}
