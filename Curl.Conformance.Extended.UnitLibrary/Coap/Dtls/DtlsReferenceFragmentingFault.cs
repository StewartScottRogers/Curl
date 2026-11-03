namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Fragments every message of the hello flight into pieces of at most <paramref name="fragmentLength" /> body bytes,
/// each in its own record (RFC 6347 section 4.2.3), and sends the records last first, so curl must queue the later
/// messages (section 4.2.2) and reassemble each one from its fragments.
/// </summary>
/// <param name="fragmentLength">The most body bytes one fragment carries.</param>
public sealed class DtlsReferenceFragmentingFault(int fragmentLength) : DtlsReferenceFault
{
    private const int HandshakeHeaderLength = 12;

    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendHelloFlight(IReadOnlyList<byte[]> messages, DtlsReferenceServer server)
    {
        List<DtlsReferenceRecord> records = [.. messages.SelectMany(Fragment).Select(fragment => server.PlainRecord(DtlsReferenceRecord.Handshake, fragment))];
        records.Reverse();
        return records;
    }

    /// <summary>Cuts a message into fragments: each keeps the message's type, length and message_seq and gives its own fragment_offset and fragment_length.</summary>
    private IEnumerable<byte[]> Fragment(byte[] message)
    {
        int bodyLength = message.Length - HandshakeHeaderLength;
        for (int offset = 0; offset == 0 || offset < bodyLength; offset += fragmentLength)
        {
            int length = Math.Min(fragmentLength, bodyLength - offset);
            yield return [.. message.AsSpan(0, 6), .. UInt24(offset), .. UInt24(length), .. message.AsSpan(HandshakeHeaderLength + offset, length)];
        }
    }

    private static byte[] UInt24(int value) => [(byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
