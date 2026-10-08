using System.IO.Compression;

namespace Curl.Protocol.Ssh.Compression;

/// <summary>
/// Inflates one direction's payloads from one zlib stream (RFC 1950) that lasts the
/// session: each packet's bytes continue the stream, and everything they complete is that
/// packet's payload, whether the peer ended them with a sync flush or, as OpenSSH does, a
/// partial flush.
/// </summary>
internal sealed class SshZlibDecompressor
{
    /// <summary>
    /// The largest payload one packet may inflate to: libssh2 1.11.1's
    /// <c>LIBSSH2_PACKET_MAXPAYLOAD</c>, past which it reports excessive growth.
    /// </summary>
    internal const int MaximumPayloadLength = 40000;

    private readonly MemoryStream input = new();

    private readonly ZLibStream inflater;

    private readonly byte[] block = new byte[4096];

    /// <summary>
    /// Initializes a new instance of the <see cref="SshZlibDecompressor" /> class.
    /// </summary>
    internal SshZlibDecompressor() => inflater = new ZLibStream(input, CompressionMode.Decompress);

    /// <summary>
    /// Inflates the stream's next piece.
    /// </summary>
    /// <param name="compressed">The bytes a packet carries in place of its payload.</param>
    /// <returns>The payload, starting with its message number.</returns>
    /// <exception cref="InvalidDataException">
    /// The bytes are not a valid continuation of the stream, complete no payload byte, or
    /// inflate to more than <see cref="MaximumPayloadLength" /> bytes.
    /// </exception>
    internal byte[] Decompress(byte[] compressed)
    {
        input.SetLength(0);
        input.Write(compressed);
        input.Position = 0;
        using MemoryStream payload = new();
        int read;
        while ((read = ReadInflated()) > 0)
        {
            payload.Write(block, 0, read);
            if (payload.Length > MaximumPayloadLength)
            {
                throw new InvalidDataException($"The SSH packet inflates to more than {MaximumPayloadLength} bytes.");
            }
        }

        return payload.Length > 0 ? payload.ToArray() : throw new InvalidDataException("The SSH packet inflates to no payload.");
    }

    /// <summary>
    /// Reads the inflater's next block, turning the BCL's internal <c>ZLibException</c> - an
    /// <see cref="IOException" /> it throws for a header asking for a preset dictionary, among
    /// others - into the <see cref="InvalidDataException" /> every other corrupt stream raises.
    /// </summary>
    /// <returns>The number of bytes read into the block; 0 once the piece is spent.</returns>
    /// <exception cref="InvalidDataException">The bytes are not a valid continuation of the stream.</exception>
    private int ReadInflated()
    {
        try
        {
            return inflater.Read(block);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("The SSH packet is not a valid continuation of the zlib stream.", exception);
        }
    }
}
