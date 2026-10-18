# ADR-0691: The RTSP handler makes libcurl's RTSP requests as curl 8.22.0's `lib/rtsp.c` does

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

`RtspProtocolHandler` only sent the curl tool's `OPTIONS *` with a fresh `CSeq` counter per transfer.
A libcurl caller (BL-2704) picks the request with `CURLOPT_RTSP_REQUEST` and sets the stream URI,
`Transport`, session ID, client `CSeq` and a body, and libcurl keeps the `CSeq` counter and session
ID on the easy handle from perform to perform. BL-2751's text, written from an older libcurl,
expected a "Refusing to issue an RTSP request [%s] without a session ID." refusal. curl 8.22.0's
`lib/rtsp.c` (tag `curl-8_22_0`, read on 2026-10-18) has no such check.

## Decision

1. The handler takes an optional `RtspRequestSettings` (kind, stream URI, `Transport`, session ID,
   client `CSeq`, body) and an optional `RtspSessionState` that the engine keeps on the easy handle.
   With neither, the tool's transfer is unchanged. The library defines both types itself, so it
   needs no reference to `Curl.Api.UnitLibrary`.
2. Header order follows `rtsp_do`: request line, `CSeq`, `Session` (only when an ID is held),
   `Transport` (a `SETUP` only), `Accept: application/sdp` (a `DESCRIBE` only), `Range`,
   `Referer`, `User-Agent`, `Authorization`, the custom headers, then for an `ANNOUNCE`,
   `GET_PARAMETER` or `SET_PARAMETER` with a non-empty body `Content-Length` and `Content-Type`
   (`application/sdp` or `text/parameters`), the blank line and the body. A custom header of the
   same name suppresses each of curl's own headers. Other kinds send no body.
3. A `SETUP` with neither a `Transport` setting nor a custom `Transport` header fails with 43,
   `Refusing to issue an RTSP SETUP without a Transport: header.`, after connecting and before the
   `CSeq` and `Session` custom header checks. A custom `Transport` header is sent among the custom
   headers instead of curl's own.
4. There is no session-ID refusal: any kind is sent without a session ID, with no `Session` header,
   as 8.22.0 does. BL-2751's criterion was corrected to say so before any code was written.
5. A libcurl request sends `Range` only on `PLAY`, `PAUSE` and `RECORD` (`rtsp_setup_request`). The
   tool's `OPTIONS` keeps the `Range` measured in BL-1293.
6. A libcurl request sends no default `User-Agent`, since libcurl sets none. The tool keeps
   `curl/8.22.0`.
7. The `CSeq` moves on only after the request was sent. A new state's counter is 0. On a new
   connection a 0 becomes 1, as `rtsp_connect` does, so the tool's reused connection still sends
   `CSeq: 0`. A set client `CSeq` or session ID takes effect at the start of the perform that
   carries it.
8. An empty `GET_PARAMETER` (a heartbeat) leaves the reply body unread, as libcurl's `no_body` does.
   A `RECEIVE` sends nothing, and its connection is not kept, since the server ended it by closing.

## Consequences

The engine (BL-2704) maps `EasyRtspOptions` to `RtspRequestSettings` and keeps one
`RtspSessionState` per easy handle. Two things are left out and filed as their own task: the
`Accept-Encoding` header a `DESCRIBE` sends under `CURLOPT_ACCEPT_ENCODING`, and the time
condition headers on `SETUP` and `DESCRIBE`.
