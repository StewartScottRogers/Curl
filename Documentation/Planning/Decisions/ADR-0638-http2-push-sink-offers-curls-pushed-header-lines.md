# ADR-0638 — HTTP/2 server push reaches an `IHttp2PushSink` with curl's `name:value` header lines

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2516.

## Context

BL-2515 taught `Curl.Http2`'s `Http2Connection` to accept `PUSH_PROMISE` when `IsPushEnabled`. BL-2516
carries it into the HTTP handler: a handler given a push sink must run its HTTP/2 connections with
push enabled and offer each promise to the sink, as curl 8.22.0's `lib/http2.c` (`push_promise`,
`on_header`, `on_frame_recv`) offers one to a multi handle's `CURLMOPT_PUSHFUNCTION`. The task's
goal text describes the header lines as `name: value`; curl's own list, read by
`curl_pushheader_bynum`, is built with `curl_maprintf("%s:%s", name, value)`, with no space.

## Decision

- `Curl.Original.Protocol.Http` gains two public interfaces: `IHttp2PushSink.TryAcceptPush(headers, response)`,
  synchronous, answering take or refuse, and `IHttpPushedResponse` (`StreamId`, and `Content`, a
  read-only, asynchronous-only `Stream` giving `HTTP/2 200 \r\n`, the headers, the empty line, then
  the DATA, as `Http2StreamConnection` presents any response). `Curl.Protocol.Abstractions` does not
  change.
- The sink comes in through `HttpProtocolHandler`'s constructor (`http2PushSink`, last and optional),
  as other per-transfer hooks do. With one, a new HTTP/2 session sends SETTINGS_ENABLE_PUSH 1; without
  one, 0 and the measured preface bytes, unchanged. An h2c upgrade keeps ENABLE_PUSH 0: its
  `HTTP2-Settings` value is the measured one.
- Each request stream carries its transfer's sink. A promise on a stream with no sink, or on a stream
  no transfer owns, or one the sink refuses, is reset with `RST_STREAM` `CANCEL`, as curl resets a
  push its callback denies. The promise's header block is HPACK-decoded first either way, so the
  connection's decoder stays in step.
- **The header lines match curl, not the goal text's wording:** one `name:value` string per field,
  pseudo-headers included, in arrival order - what `curl_pushheader_bynum` hands a push callback.
  Matching curl's bytes is the standing rule, and a later libcurl `CURLMOPT_PUSHFUNCTION` can hand
  these strings over unchanged.
- The sink is called while the session holds its frame lock, so it must not read the pushed response
  before returning; frames for a taken push are routed to its stream and read later.

## Consequences

A later task can wire a `CURLMOPT_PUSHFUNCTION` in `Libcurl` (or any host) to an `IHttp2PushSink`
through `EngineComposition` without touching the HTTP library again.
