namespace Curl.Original.Protocol.Ws;

/// <summary>
/// The frames of a WebSocket upload that a read callback feeds, as curl 8.22.0's <c>ws->enc</c> and
/// <c>ws->sendbuf</c> are for its <c>cr_ws_read</c> client reader (BL-2611, ADR-0669). While
/// <see cref="WsProtocolHandler" /> sends the upload it is open, and <see cref="StartFrame" /> (libcurl's
/// <c>curl_ws_start_frame</c>, called from inside the read callback) writes a frame's head announcing its whole
/// payload; the bytes the callback gives next fill that frame, each read asked for no more than the payload left.
/// Bytes read with no frame being filled each become one binary frame of their length.
/// </summary>
public sealed class WsUploadFrames
{
    /// <summary>The heads started and not yet sent, as the head <c>curl_ws_start_frame</c> leaves in <c>ws->sendbuf</c>.</summary>
    private byte[] startedHeads = [];

    /// <summary>The mask of the frame being filled; zeros before the first, when no payload is masked with it.</summary>
    private byte[] mask = new byte[WsFrameEncoder.MaskLength];

    /// <summary>The payload bytes of the frame being filled still to come, as <c>ws->enc.payload_remain</c>.</summary>
    private long payloadLeft;

    /// <summary>The payload bytes of the frame being filled already encoded, which places the next part's mask.</summary>
    private long payloadOffset;

    private bool isOpen;

    /// <summary>
    /// Gets a value indicating whether a frame may start now: the upload is being sent and the frame before has no
    /// payload left, as <c>curl_ws_start_frame</c> refuses an unfinished frame.
    /// </summary>
    public bool CanStartFrame => isOpen && payloadLeft == 0;

    /// <summary>Gets a value indicating whether the last data frame started left its message unfinished.</summary>
    public bool ContinuesMessage { get; private set; }

    /// <summary>
    /// Starts a frame whose payload the next bytes read fill: writes its masked head announcing
    /// <paramref name="frameLength" /> bytes, sent before them. Call only while <see cref="CanStartFrame" />.
    /// </summary>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="frameLength">The frame's whole payload size, not negative.</param>
    /// <param name="randomSource">Supplies the frame's mask.</param>
    /// <exception cref="ArgumentNullException"><paramref name="randomSource" /> is <see langword="null" />.</exception>
    public void StartFrame(WsOpcode opcode, bool isFinal, long frameLength, IWebSocketRandomSource randomSource)
    {
        byte[] head = WsFrameEncoder.EncodeHead(opcode, isFinal, frameLength, randomSource);
        startedHeads = [.. startedHeads, .. head];
        mask = head[^WsFrameEncoder.MaskLength..];
        payloadLeft = frameLength;
        payloadOffset = 0;
        ContinuesMessage = opcode is WsOpcode.Close or WsOpcode.Ping or WsOpcode.Pong ? ContinuesMessage : !isFinal;
    }

    /// <summary>Opens the frames for an upload being sent, forgetting any frame of an earlier one.</summary>
    internal void Open()
    {
        Reset();
        isOpen = true;
    }

    /// <summary>Closes the frames once the upload is sent, so no frame starts until the next one.</summary>
    internal void Close()
    {
        Reset();
        isOpen = false;
    }

    /// <summary>The most the next read may give: the payload left of the frame being filled, else <paramref name="bufferLength" />.</summary>
    /// <param name="bufferLength">The read buffer's length.</param>
    /// <returns>How many bytes to ask for.</returns>
    internal int ReadLength(int bufferLength) => payloadLeft > 0 ? (int)Math.Min(bufferLength, payloadLeft) : bufferLength;

    /// <summary>
    /// Encodes the bytes a read gave, as <c>cr_ws_read</c> does: any head started since the last read, then the bytes
    /// masked as the frame being filled, as many as its payload left holds; with no frame started or being filled,
    /// bytes read start a binary frame of their length first.
    /// </summary>
    /// <param name="read">The bytes the read gave.</param>
    /// <param name="randomSource">Supplies a binary frame's mask.</param>
    /// <param name="startsBinaryFrame">Whether the bytes started a binary frame.</param>
    /// <returns>The bytes to send, empty when there is nothing to send.</returns>
    internal byte[] Encode(ReadOnlySpan<byte> read, IWebSocketRandomSource randomSource, out bool startsBinaryFrame)
    {
        startsBinaryFrame = read.Length > 0 && payloadLeft == 0 && startedHeads.Length == 0;
        if (startsBinaryFrame)
        {
            StartFrame(WsOpcode.Binary, isFinal: true, read.Length, randomSource);
        }

        int fits = (int)Math.Min(read.Length, payloadLeft);
        byte[] bytes = [.. startedHeads, .. WsFrameEncoder.MaskPart(read[..fits], mask, payloadOffset)];
        startedHeads = [];
        payloadOffset += fits;
        payloadLeft -= fits;
        return bytes;
    }

    private void Reset()
    {
        startedHeads = [];
        payloadLeft = 0;
        payloadOffset = 0;
        ContinuesMessage = false;
    }
}
