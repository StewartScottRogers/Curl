using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>
/// Keeps every setting of every SETTINGS frame the server sends, in order, since <see cref="Http2Connection" />
/// applies only the settings RFC 9113 defines and drops SETTINGS_ENABLE_CONNECT_PROTOCOL (RFC 8441 section 3).
/// </summary>
internal sealed class PeerSettingsRecorder : IHttp2FrameObserver
{
    private readonly List<Http2Setting> settings = [];

    /// <summary>Gets the settings the server has sent so far, in the order it sent them.</summary>
    internal IReadOnlyList<Http2Setting> Settings => settings;

    /// <inheritdoc />
    public void FrameSent(Http2Frame frame)
    {
    }

    /// <inheritdoc />
    public void FrameReceived(Http2Frame frame)
    {
        if (frame.Type == Http2FrameType.Settings && !frame.HasFlag(Http2FrameFlags.Acknowledgement))
        {
            settings.AddRange(Http2FramePayloadParser.ParseSettings(frame));
        }
    }
}
