using System.IO.Compression;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>
/// server.py's compressors made with the BCL (ADR-0784): <c>gz</c> (gzip at level 9), <c>zlib.compress</c>
/// (zlib at the default level) and a raw deflate at level 9. The deflate bytes are .NET's, not Python's;
/// the framing matches: a gzip header with mtime 0, extra flags 2 and OS 3 as Python's zlib writes it.
/// </summary>
public static class ServerPyCompression
{
    private const int GzipExtraFlagsOffset = 8;

    private const int GzipOperatingSystemOffset = 9;

    /// <summary>server.py's <c>gz(content)</c>: a gzip member at level 9.</summary>
    public static byte[] Gzip(byte[] content)
    {
        var member = Compress(output => new GZipStream(output, CompressionLevel.SmallestSize), content);
        member[GzipExtraFlagsOffset] = 2;
        member[GzipOperatingSystemOffset] = 3;
        return member;
    }

    /// <summary>server.py's <c>zlib.compress(content)</c>: a zlib stream at the default level.</summary>
    public static byte[] Zlib(byte[] content) => Compress(output => new ZLibStream(output, CompressionLevel.Optimal), content);

    /// <summary>server.py's <c>compressobj(9, DEFLATED, -15)</c>: a raw deflate stream at level 9.</summary>
    public static byte[] RawDeflate(byte[] content) => Compress(output => new DeflateStream(output, CompressionLevel.SmallestSize), content);

    private static byte[] Compress(Func<Stream, Stream> openCompressor, byte[] content)
    {
        using var output = new MemoryStream();
        using (var compressor = openCompressor(output))
        {
            compressor.Write(content);
        }

        return output.ToArray();
    }
}
