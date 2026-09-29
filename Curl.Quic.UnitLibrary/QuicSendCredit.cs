namespace Curl.Quic;

/// <summary>
/// How much this endpoint may send under one of the peer's flow control limits, for one
/// stream (MAX_STREAM_DATA) or for the whole connection (MAX_DATA): the limit, the bytes
/// used against it, and whether the BLOCKED frame for the current limit has gone out
/// (RFC 9000 sections 4.1 and 19.12 to 19.13).
/// </summary>
/// <param name="limit">The peer's initial limit.</param>
internal sealed class QuicSendCredit(ulong limit)
{
    private ulong? blockedReportedAt;

    /// <summary>Gets the peer's limit: the offset, or total, this endpoint may not send beyond.</summary>
    public ulong Limit { get; private set; } = limit;

    /// <summary>Gets the bytes sent against the limit.</summary>
    public ulong Used { get; private set; }

    /// <summary>Gets how many more bytes may be sent.</summary>
    public ulong Available => Limit - Used;

    /// <summary>Raises the limit, as a MAX_DATA or MAX_STREAM_DATA frame asks; a lower one is ignored (RFC 9000 section 4.1).</summary>
    /// <param name="newLimit">The limit the frame carries.</param>
    public void Raise(ulong newLimit) => Limit = Math.Max(Limit, newLimit);

    /// <summary>Records bytes sent against the limit.</summary>
    /// <param name="length">How many.</param>
    public void Use(ulong length) => Used += length;

    /// <summary>Returns <see langword="true" /> once per limit when every byte of it is used, so the BLOCKED frame naming it goes out once (RFC 9000 section 4.1).</summary>
    /// <returns>Whether a BLOCKED frame is due now.</returns>
    public bool TakeBlockedReport()
    {
        if (Available > 0 || blockedReportedAt == Limit)
        {
            return false;
        }

        blockedReportedAt = Limit;
        return true;
    }
}
