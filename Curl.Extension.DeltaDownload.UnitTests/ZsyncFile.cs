using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

/// <summary>Writes a zsync 0.6 control file for a target, as zsyncmake does: headers, an empty line, then each block's checksums.</summary>
internal static class ZsyncFile
{
    public static byte[] For(byte[] target, int blockSize, string hashLengths = "1,4,16", string? fileName = "file.bin")
    {
        var lengths = hashLengths.Split(',').Select(int.Parse).ToArray();
        using MemoryStream file = new();
        var headers = $"zsync: 0.6.2\n{(fileName is null ? string.Empty : $"Filename: {fileName}\n")}MTime: Wed, 28 Oct 2026 12:00:00 +0000\nBlocksize: {blockSize}\nLength: {target.Length}\nHash-Lengths: {hashLengths}\nURL: file.bin\nSHA-1: {Convert.ToHexStringLower(SHA1.HashData(target))}\n\n";
        file.Write(Encoding.Latin1.GetBytes(headers));
        for (var offset = 0; offset < target.Length; offset += blockSize)
        {
            var block = new byte[blockSize];
            target.AsSpan(offset, Math.Min(blockSize, target.Length - offset)).CopyTo(block);
            var weak = RollingChecksum.Of(block).Value;
            byte[] weakBytes = [(byte)(weak >> 24), (byte)(weak >> 16), (byte)(weak >> 8), (byte)weak];
            file.Write(weakBytes.AsSpan(4 - lengths[1]));
            file.Write(Md4.Hash(block).AsSpan(0, lengths[2]));
        }

        return file.ToArray();
    }
}
