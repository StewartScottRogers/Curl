namespace Curl.Quic;

/// <summary>One connection ID an endpoint issued (RFC 9000 section 5.1), with its sequence number and the stateless reset token that goes with it.</summary>
/// <param name="SequenceNumber">The sequence number: 0 for the one in the handshake, then one more for each NEW_CONNECTION_ID.</param>
/// <param name="ConnectionId">The connection ID.</param>
/// <param name="StatelessResetToken">The 16-byte stateless reset token, or <see langword="null" /> when none was given (the handshake's own ID before the server's transport parameters).</param>
public sealed record QuicConnectionIdEntry(ulong SequenceNumber, byte[] ConnectionId, byte[]? StatelessResetToken);
