namespace Curl.Http2;

/// <summary>
/// Is told of every frame an <see cref="Http2Connection" /> writes or reads, as it crosses the
/// wire, so a caller can trace the framing as curl's <c>--trace-config http/2</c> does (BL-1167).
/// </summary>
public interface IHttp2FrameObserver
{
    /// <summary>Called once a frame has been written.</summary>
    /// <param name="frame">The frame written.</param>
    void FrameSent(Http2Frame frame);

    /// <summary>Called once a frame has been read, before the connection acts on it.</summary>
    /// <param name="frame">The frame read.</param>
    void FrameReceived(Http2Frame frame);
}
