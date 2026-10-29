namespace Curl.WebSocket;

/// <summary>What <see cref="WsFrameMetadataDecoder" /> made of one run of received bytes (BL-2471).</summary>
/// <param name="Runs">
/// Every run of payload in the bytes, in order, ping, pong and close frames included, each with its
/// frame's metadata.
/// </param>
/// <param name="CompletedPing">
/// The payload of the last ping completed in these bytes, to be echoed in a pong as curl's
/// auto-pong does, or <see langword="null" /> when none completed.
/// </param>
/// <param name="ViolationMessage">
/// curl 8.21.0's message for the protocol violation that stopped decoding, such as
/// <c>[WS] masked input frame</c>, which fails the transfer with exit 56; or <see langword="null" />.
/// <paramref name="Runs" /> then holds what was decoded before it.
/// </param>
public sealed record WsDecodedFrames(IReadOnlyList<WsFrameRun> Runs, ReadOnlyMemory<byte>? CompletedPing, string? ViolationMessage);
