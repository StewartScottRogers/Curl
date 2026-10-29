namespace Curl.WebSocket;

/// <summary>
/// One whole WebSocket message an <see cref="IWsUploadMessageSource" /> uploads: the ws handler sends it as one
/// final masked frame of <paramref name="Opcode" /> carrying <paramref name="Payload" /> (BL-4253).
/// </summary>
/// <param name="Opcode">The frame's opcode, <see cref="WsOpcode.Text" /> or <see cref="WsOpcode.Binary" />.</param>
/// <param name="Payload">The message's bytes, the frame's whole payload.</param>
public sealed record WsUploadMessage(WsOpcode Opcode, ReadOnlyMemory<byte> Payload);
