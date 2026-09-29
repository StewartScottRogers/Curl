using System.Buffers.Binary;

namespace Curl.Networking;

/// <summary>
/// The query <see cref="DnsServerResolver" /> sends a DNS server, as curl 8.22.0's c-ares 1.34.8
/// build sends it (measured, BL-694): <see cref="DnsQueryEncoder" />'s header and question with
/// the query's ID, and one EDNS(0) OPT record (RFC 6891) advertising a 1232-byte UDP payload,
/// carrying an 8-byte client DNS cookie (RFC 7873) over UDP and no option over TCP. It also
/// matches a reply to its query and reads the reply's answer through <see cref="DnsAnswerDecoder" />.
/// </summary>
public static class DnsServerQuery
{
    /// <summary>The length of the client cookie carried over UDP.</summary>
    public const int ClientCookieLength = 8;

    private const int HeaderLength = 12;

    /// <summary>
    /// Builds the query for <paramref name="name" /> and <paramref name="recordType" />.
    /// </summary>
    /// <param name="name">The name, already in its ASCII form.</param>
    /// <param name="recordType">The record type asked for.</param>
    /// <param name="id">The query's ID.</param>
    /// <param name="clientCookie">The 8-byte client cookie for a UDP query; empty for a TCP one.</param>
    /// <returns>The query bytes, or the failure <see cref="DnsQueryEncoder" /> reported.</returns>
    public static DnsQueryEncoding Build(string name, DnsRecordType recordType, ushort id, ReadOnlySpan<byte> clientCookie)
    {
        var encoded = DnsQueryEncoder.Encode(name, recordType);
        if (encoded.Failure != DnsMessageFailure.None)
        {
            return encoded;
        }

        var optionLength = clientCookie.IsEmpty ? 0 : 4 + clientCookie.Length;
        var query = new byte[encoded.Bytes.Length + 11 + optionLength];
        encoded.Bytes.CopyTo(query, 0);
        BinaryPrimitives.WriteUInt16BigEndian(query, id);
        query[11] = 1;
        var opt = query.AsSpan(encoded.Bytes.Length);
        opt[2] = 41;
        BinaryPrimitives.WriteUInt16BigEndian(opt[3..], 1232);
        BinaryPrimitives.WriteUInt16BigEndian(opt[9..], (ushort)optionLength);
        if (optionLength > 0)
        {
            opt[12] = 10;
            opt[14] = (byte)clientCookie.Length;
            clientCookie.CopyTo(opt[15..]);
        }

        return new DnsQueryEncoding(query, DnsMessageFailure.None);
    }

    /// <summary>
    /// Tells whether <paramref name="reply" /> answers <paramref name="query" />: a response with
    /// the query's ID and the query's question, its name compared ignoring ASCII case, as c-ares
    /// matches one; and whether the server truncated it (the TC flag).
    /// </summary>
    /// <param name="query">The query as sent.</param>
    /// <param name="reply">The reply received.</param>
    /// <returns>How the reply matches.</returns>
    public static DnsReplyMatch Match(ReadOnlySpan<byte> query, ReadOnlySpan<byte> reply)
    {
        var questionEnd = QuestionEnd(query);
        var matches = reply.Length >= questionEnd
            && reply[..2].SequenceEqual(query[..2])
            && (reply[2] & 0x80) != 0
            && AsciiEqualsIgnoringCase(reply[HeaderLength..questionEnd], query[HeaderLength..questionEnd]);
        if (!matches)
        {
            return DnsReplyMatch.Mismatch;
        }

        return (reply[2] & 0x02) != 0 ? DnsReplyMatch.Truncated : DnsReplyMatch.Complete;
    }

    /// <summary>
    /// Reads a matched reply: its response code as a <see cref="DnsLookupFailure" />, and for
    /// NOERROR the answer <see cref="DnsAnswerDecoder" /> decodes, which must hold a record of the
    /// type asked for. An answer without one, or holding a record of another type or class, is
    /// <see cref="DnsLookupFailure.NoData" />; one that does not decode is <see cref="DnsLookupFailure.BadReply" />.
    /// </summary>
    /// <param name="reply">A reply <see cref="Match" /> found <see cref="DnsReplyMatch.Complete" />.</param>
    /// <param name="recordType">The record type asked for.</param>
    /// <returns>The answer, or why there is none.</returns>
    public static DnsQueryOutcome Read(ReadOnlySpan<byte> reply, DnsRecordType recordType)
    {
        var responseCode = reply[3] & 0x0F;
        if (responseCode != 0)
        {
            return new DnsQueryOutcome(null, FailureOfResponseCode(responseCode));
        }

        // DnsAnswerDecoder takes DoH's ID 0; the ID was already matched.
        var anonymous = reply.ToArray();
        anonymous[0] = 0;
        anonymous[1] = 0;
        var answer = DnsAnswerDecoder.Decode(anonymous, recordType);
        return answer.Failure switch
        {
            DnsMessageFailure.None when answer.Addresses.Count > 0 || answer.ServiceRecords.Count > 0 => new DnsQueryOutcome(answer, DnsLookupFailure.None),
            DnsMessageFailure.None or DnsMessageFailure.NoContent or DnsMessageFailure.UnexpectedType or DnsMessageFailure.UnexpectedClass
                => new DnsQueryOutcome(null, DnsLookupFailure.NoData),
            _ => new DnsQueryOutcome(null, DnsLookupFailure.BadReply),
        };
    }

    private static DnsLookupFailure FailureOfResponseCode(int responseCode) => responseCode switch
    {
        1 => DnsLookupFailure.FormatError,
        3 => DnsLookupFailure.NotFound,
        4 => DnsLookupFailure.NotImplemented,
        5 => DnsLookupFailure.Refused,
        _ => DnsLookupFailure.ServerFailure,
    };

    /// <summary>The end of the question: the header, the name up to its root byte, and QTYPE and QCLASS.</summary>
    private static int QuestionEnd(ReadOnlySpan<byte> query)
    {
        var index = HeaderLength;
        while (query[index] != 0)
        {
            index += 1 + query[index];
        }

        return index + 1 + 4;
    }

    private static bool AsciiEqualsIgnoringCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        for (var index = 0; index < left.Length; index++)
        {
            if (FoldAsciiLetter(left[index]) != FoldAsciiLetter(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte FoldAsciiLetter(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value | 0x20) : value;
}
