using System.IO.Compression;

namespace Curl.Protocol.Ssh.Compression;

/// <summary>
/// Compresses one direction's payloads into one zlib stream (RFC 1950) that lasts the
/// session, each payload ending with a sync flush so the peer can inflate all of it from
/// that packet alone (RFC 4253 section 6.2). A sync flush is the BCL's
/// <see cref="ZLibStream.Flush()" />; OpenSSH inflates it as it inflates its own partial
/// flushes.
/// </summary>
internal sealed class SshZlibCompressor
{
    private readonly MemoryStream output = new();

    private readonly ZLibStream deflater;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshZlibCompressor" /> class at zlib's
    /// default level, as libssh2 compresses.
    /// </summary>
    internal SshZlibCompressor() => deflater = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true);

    /// <summary>
    /// Compresses <paramref name="payload" /> as the stream's next piece.
    /// </summary>
    /// <param name="payload">The payload, starting with its message number.</param>
    /// <returns>The compressed bytes that go into the packet in its place.</returns>
    internal byte[] Compress(ReadOnlySpan<byte> payload)
    {
        output.SetLength(0);
        deflater.Write(payload);
        deflater.Flush();
        return output.ToArray();
    }
}
