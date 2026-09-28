namespace Curl.Protocol.Ws;

/// <summary>What <see cref="WsFrameDecoder" /> made of one run of received bytes.</summary>
/// <param name="Payload">
/// The payload bytes of every text, binary, continuation, close and pong frame, in order: what
/// curl writes to the output. Ping payloads are never included.
/// </param>
/// <param name="LastPing">
/// The payload of the last ping completed in these bytes, to be echoed in a pong, or
/// <see langword="null" /> when none completed.
/// </param>
/// <param name="Failure">
/// The protocol violation that stopped decoding, or <see langword="null" />. <paramref name="Payload" />
/// then holds what was decoded before it, which curl has already written.
/// </param>
internal sealed record WsDecodedBytes(byte[] Payload, byte[]? LastPing, WsTransferException? Failure);
