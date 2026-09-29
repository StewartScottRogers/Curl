namespace Curl.Quic;

/// <summary>
/// One ACK Range after the first in an ACK frame (RFC 9000 section 19.3.1).
/// </summary>
/// <param name="Gap">One less than the number of unacknowledged packets below the previous range.</param>
/// <param name="Length">One less than the number of acknowledged packets in this range.</param>
public readonly record struct QuicAckRange(ulong Gap, ulong Length);
