namespace Curl.WebSocket;

/// <summary>
/// One run of a received frame's payload as <see cref="WsFrameMetadataDecoder" /> passes it on,
/// with what libcurl's <c>curl_ws_meta</c> reports for it (BL-2471).
/// </summary>
/// <param name="Opcode">The frame's opcode; a later fragment of a message is <see cref="WsOpcode.Continuation" />.</param>
/// <param name="IsFinal">Whether the frame's FIN bit is set: it ends its message.</param>
/// <param name="Offset">How many bytes of the frame's payload came before this run.</param>
/// <param name="BytesLeft">How many bytes of the frame's payload are still to come after this run.</param>
/// <param name="Payload">
/// The run's payload bytes, unmasked; empty for a frame with no payload, which is still reported
/// once, with <paramref name="Offset" /> and <paramref name="BytesLeft" /> 0.
/// </param>
public sealed record WsFrameRun(WsOpcode Opcode, bool IsFinal, long Offset, long BytesLeft, ReadOnlyMemory<byte> Payload);
