using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Tells, from the TLS records <see cref="System.Net.Security.SslStream" /> reads under it,
/// how many records the server sent after a TLS 1.3 handshake and before the first
/// application data that carried no application data: the <c>NewSessionTicket</c> records,
/// each of which curl 8.21.0's Schannel build answers with its three <c>schannel:</c>
/// renegotiation lines (BL-1089, ADR-0309).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Net.Security.SslStream" /> decrypts a ticket out of sight, so the records
/// are told apart by size. Every TLS 1.3 record carries its plaintext, one inner content type
/// byte and a 16-byte AEAD tag, so an application data record yields its length less 17
/// bytes of plaintext (servers do not pad), a ticket record none. The first read that returns
/// plaintext returns the plaintext of consecutive records, so the ticket records are the
/// fewest leading records after which a run of records accounts for exactly the bytes read.
/// A read that stops inside a record matches no run, and so counts no ticket.
/// </para>
/// <para>
/// It looks only until that first read: a ticket that arrives later goes unreported.
/// </para>
/// </remarks>
internal sealed class SessionTicketRecordDetector
{
    private const int RecordHeaderLength = 5;

    private const int Tls13RecordOverhead = 17;

    private static readonly TlsMessageEvent NewSessionTicketReceived = new()
    {
        ProtocolVersion = 0x0304,
        ContentType = TlsContentType.Handshake,
        Sent = false,
        Bytes = new byte[] { 4 },
    };

    private readonly byte[] _header = new byte[RecordHeaderLength];

    private readonly List<int> _plaintextLengths = [];

    private int _headerBytesRead;

    private int _bodyBytesLeft;

    private bool _handshakeComplete;

    private bool _finished;

    /// <summary>
    /// Follows the record boundaries in <paramref name="received" />, the next bytes read from
    /// the transport, queuing each record that completes after <see cref="MarkHandshakeComplete" />.
    /// </summary>
    /// <param name="received">The bytes, in the order read.</param>
    public void ObserveReceived(ReadOnlySpan<byte> received)
    {
        while (!_finished && !received.IsEmpty)
        {
            received = _bodyBytesLeft > 0 ? SkipBody(received) : ReadHeader(received);
        }
    }

    /// <summary>
    /// Marks the handshake complete: records already received belong to it and are forgotten,
    /// and every record that completes from now on is queued.
    /// </summary>
    public void MarkHandshakeComplete()
    {
        _plaintextLengths.Clear();
        _handshakeComplete = true;
    }

    /// <summary>
    /// Returns how many of the queued records were ticket records, given the first read after
    /// the handshake that returned plaintext; after it, nothing more is observed.
    /// </summary>
    /// <param name="plaintextRead">The bytes of plaintext the read returned; 0 counts nothing.</param>
    /// <returns>The number of ticket records, 0 when no run of records accounts for the read.</returns>
    public int CountTicketRecords(int plaintextRead)
    {
        if (_finished || plaintextRead == 0)
        {
            return 0;
        }

        _finished = true;
        for (int tickets = 0; tickets < _plaintextLengths.Count; tickets++)
        {
            if (RecordsFromAccountFor(tickets, plaintextRead))
            {
                return tickets;
            }
        }

        return 0;
    }

    /// <summary>
    /// Reports each ticket record <see cref="CountTicketRecords" /> finds to
    /// <paramref name="events" /> as a received <c>NewSessionTicket</c>, of which only the
    /// handshake type is known: <see cref="System.Net.Security.SslStream" /> decrypted it out of sight.
    /// </summary>
    /// <param name="plaintextRead">The bytes of plaintext the read returned.</param>
    /// <param name="events">Where the tickets are reported.</param>
    public void ReportTicketRecords(int plaintextRead, ITransferEvents events)
    {
        for (int ticketRecords = CountTicketRecords(plaintextRead); ticketRecords > 0; ticketRecords--)
        {
            events.ReportTlsMessage(NewSessionTicketReceived);
        }
    }

    private bool RecordsFromAccountFor(int first, int plaintextRead)
    {
        int plaintext = 0;
        for (int index = first; index < _plaintextLengths.Count && plaintext < plaintextRead; index++)
        {
            plaintext += _plaintextLengths[index];
        }

        return plaintext == plaintextRead;
    }

    private ReadOnlySpan<byte> SkipBody(ReadOnlySpan<byte> received)
    {
        int skipped = Math.Min(_bodyBytesLeft, received.Length);
        _bodyBytesLeft -= skipped;
        return received[skipped..];
    }

    private ReadOnlySpan<byte> ReadHeader(ReadOnlySpan<byte> received)
    {
        int copied = Math.Min(RecordHeaderLength - _headerBytesRead, received.Length);
        received[..copied].CopyTo(_header.AsSpan(_headerBytesRead));
        _headerBytesRead += copied;
        if (_headerBytesRead == RecordHeaderLength)
        {
            _headerBytesRead = 0;
            _bodyBytesLeft = (_header[3] << 8) | _header[4];
            QueueRecordIfAfterHandshake(_bodyBytesLeft);
        }

        return received[copied..];
    }

    // A record is queued at its header: SslStream decrypts it only once it has all of it, by
    // which time the queue is read no earlier than the plaintext it yields.
    private void QueueRecordIfAfterHandshake(int recordLength)
    {
        if (_handshakeComplete)
        {
            _plaintextLengths.Add(Math.Max(0, recordLength - Tls13RecordOverhead));
        }
    }
}
