using System.Buffers.Binary;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Reads whole LDAPMessages from a connection: the identifier, the definite length and then
/// the content, however the bytes are split across reads (ADR-0166).
/// </summary>
/// <param name="connection">The connection to the LDAP server.</param>
internal sealed class LdapMessageReader(IConnection connection)
{
    /// <summary>The frame length that means more bytes are needed to know it.</summary>
    internal const int NeedMoreBytes = -1;

    /// <summary>The frame length that means the bytes cannot start an LDAPMessage.</summary>
    internal const int MalformedFrame = -2;

    /// <summary>An LDAPMessage's identifier octet: a constructed universal SEQUENCE.</summary>
    private const byte SequenceIdentifier = 0x30;

    /// <summary>The most length octets a long-form length may have here: four, as WinLDAP writes.</summary>
    private const int MaxLengthOctets = 4;

    private byte[] buffer = new byte[4096];

    private int count;

    /// <summary>Reads the next whole LDAPMessage.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The status, and with <see cref="LdapReadStatus.Message" /> the message, identifier and
    /// length octets included; an empty array otherwise.
    /// </returns>
    public async ValueTask<(LdapReadStatus Status, byte[] Message)> ReadMessageAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            int frameLength = MeasureFrame(buffer.AsSpan(0, count));
            if (frameLength == MalformedFrame)
            {
                return (LdapReadStatus.Malformed, []);
            }

            if (frameLength != NeedMoreBytes && frameLength <= count)
            {
                byte[] message = buffer[..frameLength];
                count -= frameLength;
                Array.Copy(buffer, frameLength, buffer, 0, count);
                return (LdapReadStatus.Message, message);
            }

            if (count == buffer.Length)
            {
                Array.Resize(ref buffer, (int)Math.Min(buffer.Length * 2L, Array.MaxLength));
            }

            int read = await connection.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return (LdapReadStatus.Closed, []);
            }

            count += read;
        }
    }

    /// <summary>Measures the LDAPMessage that <paramref name="bytes" /> start with.</summary>
    /// <param name="bytes">The bytes read so far.</param>
    /// <returns>
    /// The message's whole length, identifier and length octets included;
    /// <see cref="NeedMoreBytes" /> when the length octets have not all arrived; or
    /// <see cref="MalformedFrame" /> when the first octet is not a SEQUENCE's, the length is
    /// indefinite or has more than four octets, or the message could not fit in an array.
    /// </returns>
    internal static int MeasureFrame(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return NeedMoreBytes;
        }

        if (bytes[0] != SequenceIdentifier)
        {
            return MalformedFrame;
        }

        if (bytes.Length < 2)
        {
            return NeedMoreBytes;
        }

        int first = bytes[1];
        return first < 0x80 ? 2 + first : MeasureLongFormFrame(bytes, first & 0x7F);
    }

    /// <summary>Measures an LDAPMessage whose length is in the long form.</summary>
    /// <param name="bytes">The bytes read so far, the identifier and first length octet among them.</param>
    /// <param name="lengthOctets">How many length octets follow the first.</param>
    /// <returns>As <see cref="MeasureFrame(ReadOnlySpan{byte})" />.</returns>
    private static int MeasureLongFormFrame(ReadOnlySpan<byte> bytes, int lengthOctets)
    {
        if (lengthOctets is 0 or > MaxLengthOctets)
        {
            return MalformedFrame;
        }

        if (bytes.Length < 2 + lengthOctets)
        {
            return NeedMoreBytes;
        }

        Span<byte> bigEndian = stackalloc byte[MaxLengthOctets];
        bigEndian.Clear();
        bytes.Slice(2, lengthOctets).CopyTo(bigEndian[(MaxLengthOctets - lengthOctets)..]);
        long frameLength = 2L + lengthOctets + BinaryPrimitives.ReadUInt32BigEndian(bigEndian);
        return frameLength > Array.MaxLength ? MalformedFrame : (int)frameLength;
    }
}
