using Curl.Extended.Protocol.Coap.Dtls.Wire;

namespace Curl.Extended.Protocol.Coap.Dtls.Handshake;

/// <summary>Splits a received datagram into the DTLS records it carries (RFC 6347 section 4.1).</summary>
internal static class DtlsDatagramRecords
{
    /// <summary>
    /// Gives each whole record of <paramref name="datagram" />, header and fragment, in order. A record
    /// whose header does not parse ends the split: it and the bytes after it are dropped, as section
    /// 4.1.2.7 drops invalid records.
    /// </summary>
    public static List<byte[]> Split(ReadOnlySpan<byte> datagram)
    {
        List<byte[]> records = [];
        int offset = 0;
        try
        {
            while (offset < datagram.Length)
            {
                int length = DtlsRecordHeader.HeaderLength + DtlsRecordHeader.Decode(datagram[offset..]).Length;
                records.Add(datagram.Slice(offset, length).ToArray());
                offset += length;
            }
        }
        catch (FormatException)
        {
        }

        return records;
    }

    /// <summary>
    /// Gives the handshake fragments one record's plaintext carries, in order (section 4.2.3); a fragment
    /// that does not parse ends the list.
    /// </summary>
    public static List<DtlsHandshakeFragment> HandshakeFragmentsOf(byte[] plaintext)
    {
        List<DtlsHandshakeFragment> fragments = [];
        int offset = 0;
        try
        {
            while (offset < plaintext.Length)
            {
                fragments.Add(DtlsHandshakeFragment.Decode(plaintext.AsSpan(offset), out int bytesRead));
                offset += bytesRead;
            }
        }
        catch (FormatException)
        {
        }

        return fragments;
    }
}
