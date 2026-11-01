namespace Curl.Original.Protocol.Ws;

/// <summary>
/// An upload stream fed by a read callback, which <see cref="WsProtocolHandler" /> frames read by read through its
/// <see cref="UploadFrames" /> as curl 8.22.0's <c>cr_ws_read</c> does, so <c>curl_ws_start_frame</c> inside the
/// callback starts the frame its bytes fill (BL-2611, ADR-0669). Any other upload fills each read before it is
/// framed, as the tool's <c>fread</c> does (BL-2683).
/// </summary>
public interface IWsUploadFrameSource
{
    /// <summary>Gets the frames the stream's bytes go out in, or <see langword="null" /> to send them as any other upload.</summary>
    WsUploadFrames? UploadFrames { get; }
}
