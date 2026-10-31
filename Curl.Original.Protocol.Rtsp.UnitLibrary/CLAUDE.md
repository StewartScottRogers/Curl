# Curl.Original.Protocol.Rtsp.UnitLibrary

Phase 5.

Real Time Streaming Protocol. HTTP-like syntax, stateful sessions with CSeq tracking.

**URL schemes:** `rtsp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

Diagnostic log (`--log-level`, ADR-0222, BL-929): `RtspTransferLog` writes under the
`rtsp` component - the request's method and `CSeq` and the reply's status, `CSeq` and
session ID (`info`), each reply header's name (`verbose`), a head the server closed before
its blank line (`warning`) - and, for every transfer, its end: bytes and milliseconds at
`info`, or its `CurlExitCode` at `error`. Header values and credentials are never written.

Interleaved RTP (BL-2477, ADR-0612): `RtspInterleavedFrameFilter` splits each `$` frame out of
the bytes read before a reply, or during a `RECEIVE` (`RtspProtocolHandler.ReceiveAsync`), and
hands it whole to the `IRtspInterleaveSink` injected into `RtspProtocolHandler`. With none, frames
before a reply are skipped as the curl tool skips them, and a `RECEIVE` writes its frames to the
body output (`RtspStreamInterleaveSink`). A sink that takes too few bytes fails with 23. Only a channel a
reply's `Transport: ...interleaved=a-b` set up (`RtspSessionState.AcceptTransport`, libcurl's `rtp_channel_mask`) is a
frame; a `$` on another is a stray byte. Frames after a reply's body go to the sink, and a frame cut off by a read is
kept as `RtspSessionState.CarriedBytes` for the connection's next request. A `RECEIVE` takes one read and stops unless a
frame is left incomplete (BL-2787).

libcurl requests (BL-2751, ADR-0691): `RtspProtocolHandler` takes optional `RtspRequestSettings`
(kind, stream URI, `Transport`, session ID, client `CSeq`, body) and an `RtspSessionState` the
engine keeps on the easy handle, so a later perform continues the `CSeq` and session ID.
`RtspRequestPlan` turns the settings into what `lib/rtsp.c` 8.22.0 sends; with no settings the
tool's `OPTIONS *` is unchanged. A `SETUP` with no `Transport` fails with 43; there is no
session-ID refusal in 8.22.0.

Reply bodies (BL-2789): a libcurl `DESCRIBE` or `GET_PARAMETER` writes its reply body to the body
output (the libcurl write callback), the kinds `lib/rtsp.c`'s `rtsp_do` clears `no_body` for
(`RtspRequestPlan.WritesReplyBody`); every other kind and the tool's `OPTIONS *` read and discard it.
