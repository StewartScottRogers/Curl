# ADR-1098 — A WebSocket over HTTP/3 waits for SETTINGS and maps FIN and reset as RFC 9220 says

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4296.

## Context

BL-4296 adds the HTTP/3 client flow of `Curl.Extension.WebSocketExtendedConnect.UnitLibrary`: a WebSocket opened
on an HTTP/3 request stream with extended CONNECT (RFC 9220). Upstream curl does not open WebSockets over HTTP/3,
so there is no curl output to match; RFC 9220 section 3 and RFC 9114 decide the behaviour. The flow sits on
`Curl.Http3`'s `Http3ClientSession` (BL-4194) and the extension's wire layer (BL-4298).

## Decision

- **Wait for SETTINGS.** `Http3ExtendedConnectClient.OpenAsync` waits for the server's SETTINGS frame (or for the
  session to stop reading the server's streams) before it sends anything on a request stream. Unless those
  SETTINGS carry SETTINGS_ENABLE_CONNECT_PROTOCOL of 1, it fails with `Http3ExtendedConnectException` and sends
  no `:protocol` request (RFC 9220 section 3).
- **The client's own SETTINGS are empty.** The client never receives extended CONNECT, so it does not advertise
  the setting; the QPACK dynamic table stays at capacity zero, as `Http3ClientSession` gives.
- **Response.** Interim 1xx responses are skipped (RFC 9114 section 4.1); the first final response opens the
  WebSocket when it is 2xx and fails naming its `:status` (or "(none)") otherwise. DATA before the response is
  dropped. A FIN before any response fails.
- **Frames.** Each WebSocket frame the client sends is masked (RFC 6455 section 5.3) and sent as one DATA frame;
  DATA payload from the server is fed to the shared `WsFrameDecoder`, whose runs are handed back. Frames other
  than DATA after the response (trailers) are ignored.
- **Closing.** The server's FIN is the orderly close (`ReceiveFramesAsync` gives `null`); a reset stream fails
  with its error code. The client's orderly close is FIN (`CloseAsync`) and its abortive close resets the stream
  with `H3_REQUEST_CANCELLED` (`Abort`), RFC 9220 section 3's mapping of TCP FIN and RST.
- **Failure cleanup.** A failed opening disposes the session, which closes and disposes the connection.

## Consequences

The HTTP/3 flow is independent of the HTTP/2 one (BL-4295); both share only the `Wire/` layer. Hooking the flow
into a scheme or a command line option is later work.
