namespace Curl.WebSocket;

/// <summary>
/// Reads RFC 6455 server frames from bytes as they arrive, keeping its place between calls, and
/// reports each run of payload with its frame's opcode, FIN bit, offset and bytes left: what
/// libcurl's <c>curl_ws_recv</c> and <c>curl_ws_meta</c> need (BL-2471).
/// </summary>
/// <remarks>
/// Decodes with the same checks, in the same order, and the same violation messages as the tool's
/// own decoding (ADR-0131), so a frame split over reads and several frames in one read decode the
/// same. After a violation the stream is broken; decode nothing more from it.
/// </remarks>
public sealed class WsFrameMetadataDecoder
{
    private readonly WsFrameDecoder decoder = new(recordsRuns: true);

    /// <summary>Decodes the next run of received bytes.</summary>
    /// <param name="received">The bytes, continuing from where the previous call ended.</param>
    /// <returns>The runs of payload with their metadata, the last completed ping, and any violation.</returns>
    public WsDecodedFrames Decode(ReadOnlySpan<byte> received)
    {
        WsDecodedBytes decoded = decoder.Decode(received);
        ReadOnlyMemory<byte>? completedPing = decoded.LastPing is { } ping ? ping : default(ReadOnlyMemory<byte>?);
        return new WsDecodedFrames(decoder.TakeRuns(), completedPing, decoded.ViolationMessage);
    }
}
