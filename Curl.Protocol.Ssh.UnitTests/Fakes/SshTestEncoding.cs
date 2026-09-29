using System.Buffers.Binary;
using System.Text;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// The RFC 4251 data types written out by hand, apart from the library's own writer, so the
/// tests compute exchange hashes and derived keys independently of the code under test.
/// </summary>
internal static class SshTestEncoding
{
    /// <summary>A <c>uint32</c>, most significant byte first.</summary>
    internal static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    /// <summary>A <c>string</c>: length, then bytes.</summary>
    internal static byte[] String(byte[] bytes) => [.. UInt32((uint)bytes.Length), .. bytes];

    /// <summary>A <c>string</c> of Latin-1 text.</summary>
    internal static byte[] Name(string text) => String(Encoding.Latin1.GetBytes(text));

    /// <summary>A non-negative <c>mpint</c> from an unsigned big-endian value.</summary>
    internal static byte[] Mpint(byte[] magnitude)
    {
        byte[] trimmed = [.. magnitude.SkipWhile(b => b == 0)];
        return String(trimmed.Length > 0 && trimmed[0] >= 0x80 ? [0, .. trimmed] : trimmed);
    }

    /// <summary>Concatenates fields.</summary>
    internal static byte[] Join(params byte[][] fields) => [.. fields.SelectMany(field => field)];

    /// <summary>
    /// Splits what the client wrote into its identification line's length and the payloads
    /// of the unencrypted packets after it.
    /// </summary>
    internal static List<byte[]> WrittenPayloads(byte[] written)
    {
        int position = Array.IndexOf(written, (byte)'\n') + 1;
        List<byte[]> payloads = [];
        while (position < written.Length)
        {
            int packetLength = (int)BinaryPrimitives.ReadUInt32BigEndian(written.AsSpan(position));
            int paddingLength = written[position + 4];
            payloads.Add(written.AsSpan(position + 5, packetLength - 1 - paddingLength).ToArray());
            position += 4 + packetLength;
        }

        return payloads;
    }
}
