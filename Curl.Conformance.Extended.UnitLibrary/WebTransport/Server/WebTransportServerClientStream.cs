using Curl.Conformance.Http3Server;

namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>One client stream as the WebTransport reference server reads it.</summary>
/// <param name="stream">The QUIC stream.</param>
internal sealed class WebTransportServerClientStream(Http3ServerStream stream)
{
    internal Http3ServerStream Stream => stream;

    internal WebTransportServerBuffer Buffer { get; } = new();

    internal WebTransportServerBuffer Capsules { get; } = new();

    internal WebTransportServerClientStreamKind Kind { get; set; }

    internal WebTransportRecordedStream? Recorded { get; set; }

    internal ulong? EchoStreamId { get; set; }

    internal bool IsHeadersRead { get; set; }

    internal bool IsFinRead { get; set; }
}
