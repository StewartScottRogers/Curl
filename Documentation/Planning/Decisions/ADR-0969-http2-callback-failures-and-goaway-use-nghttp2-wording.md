# ADR-0969: HTTP/2 flow-control and SETTINGS errors, and a GOAWAY mid-response, use nghttp2's wording

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

CGR-272: curl with nghttp2 reports a WINDOW_UPDATE overflow, an invalid SETTINGS value
and similar connection errors as exit 16 `nghttp2 recv error -902: The user callback
function failed`, and a GOAWAY that refuses the stream after the response began as exit 56
`Recv failure: Connection reset by peer`. Curl printed `nghttp2 shuts down connection with
error N: NAME` and `Failure when receiving data from the peer`.

## Decision

`Http2StreamConnection` words a FLOW_CONTROL_ERROR, and a PROTOCOL_ERROR from an invalid
SETTINGS value, as the -902 text; a GOAWAY reaching an HTTP/2 stream says
`Recv failure: Connection reset by peer`. Other protocol errors (measured in BL-658, such
as a CONTINUATION without HEADERS) and HTTP/3 refusals keep their wording. The report's
`sideven` case (a server-opened even stream) is not covered: its source in the frame layer is
not identified and it stays as before.

## Consequences

The text is the same on every platform, as the report's Windows port matched.
