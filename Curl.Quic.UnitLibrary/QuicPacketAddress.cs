namespace Curl.Quic;

/// <summary>The connection IDs and token the client's next packets carry.</summary>
/// <param name="LongHeaderDestination">The destination connection ID of Initial and Handshake packets: the server's handshake ID, or the client's random one before the server has answered.</param>
/// <param name="ShortHeaderDestination">The destination connection ID of 1-RTT packets: the server's ID in use.</param>
/// <param name="Source">The client's handshake source connection ID.</param>
/// <param name="Token">The token Initial packets carry, from a Retry or an earlier NEW_TOKEN; empty for none.</param>
internal sealed record QuicPacketAddress(byte[] LongHeaderDestination, byte[] ShortHeaderDestination, byte[] Source, ReadOnlyMemory<byte> Token);
