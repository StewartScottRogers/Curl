using System.Text;

namespace Curl.Networking;

/// <summary>
/// Encodes the DNS query curl 8.21.0 POSTs to a DNS-over-HTTPS server (ADR-0152, measured in
/// BL-639): ID 0 (RFC 8484 section 4.1), flags <c>0x0100</c> (RD only), one question for the
/// host name, no other records, QCLASS IN. It mirrors curl's <c>doh_req_encode</c>: a trailing
/// dot is allowed, an empty label or one over 63 bytes is refused, and a query over 272 bytes
/// (curl's <c>DOH_MAX_DNSREQ_SIZE</c>) is refused.
/// </summary>
public static class DnsQueryEncoder
{
    private const int HeaderLength = 12;
    private const int QuestionTypeAndClassLength = 4;
    private const int MaximumQueryLength = 256 + 16;
    private const int MaximumLabelLength = 63;

    /// <summary>Encodes the query for <paramref name="hostName" /> and <paramref name="recordType" />.</summary>
    /// <param name="hostName">The host name, already converted to its ASCII form; a trailing dot is allowed.</param>
    /// <param name="recordType">The record type asked for.</param>
    /// <returns>The query bytes, or the failure that stopped them.</returns>
    public static DnsQueryEncoding Encode(string hostName, DnsRecordType recordType)
    {
        ArgumentNullException.ThrowIfNull(hostName);

        var name = Encoding.UTF8.GetBytes(hostName);
        var length = HeaderLength + 1 + name.Length + QuestionTypeAndClassLength;
        if (name.Length > 0 && name[^1] != (byte)'.')
        {
            length++;
        }

        if (length > MaximumQueryLength)
        {
            return new DnsQueryEncoding([], DnsMessageFailure.NameTooLong);
        }

        var query = new byte[length];
        query[2] = 0x01;
        query[5] = 0x01;
        if (!TryWriteLabels(name, query.AsSpan(HeaderLength)))
        {
            return new DnsQueryEncoding([], DnsMessageFailure.BadLabel);
        }

        query[^3] = (byte)recordType;
        query[^4] = (byte)((ushort)recordType >> 8);
        query[^1] = 0x01;
        return new DnsQueryEncoding(query, DnsMessageFailure.None);
    }

    /// <summary>
    /// Writes each label of <paramref name="name" /> with its length byte; the root's zero byte is
    /// already in place. An empty name has no labels; one trailing dot ends the name.
    /// </summary>
    private static bool TryWriteLabels(ReadOnlySpan<byte> name, Span<byte> destination)
    {
        if (name.IsEmpty)
        {
            return true;
        }

        var labels = name.EndsWith((byte)'.') ? name[..^1] : name;
        var written = 0;
        var allWritten = true;
        foreach (var range in labels.Split((byte)'.'))
        {
            allWritten = allWritten && TryWriteLabel(labels[range], destination, ref written);
        }

        return allWritten;
    }

    private static bool TryWriteLabel(ReadOnlySpan<byte> label, Span<byte> destination, ref int written)
    {
        if (label.Length is 0 or > MaximumLabelLength)
        {
            return false;
        }

        destination[written] = (byte)label.Length;
        label.CopyTo(destination[(written + 1)..]);
        written += 1 + label.Length;
        return true;
    }
}
