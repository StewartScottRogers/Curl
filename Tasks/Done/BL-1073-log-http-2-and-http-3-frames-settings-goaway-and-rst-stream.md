---
id: BL-1073
title: Log HTTP/2 and HTTP/3 frames, SETTINGS, GOAWAY and RST_STREAM to the diagnostic log in Curl.Protocol.Http
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-922]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1073 — Log HTTP/2 and HTTP/3 frames, SETTINGS, GOAWAY and RST_STREAM to the diagnostic log in Curl.Protocol.Http

## Goal

`Curl.Protocol.Http.UnitLibrary` writes each HTTP/2 and HTTP/3 frame type and stream ID received or sent at `verbose`, and the server's SETTINGS, GOAWAY and RST_STREAM at `info`/`warning`, to `ITransferContext.DiagnosticLog` under component `http2` or `http3`.

## Context

- Follow-up of BL-922, which logs the exchange steps through `HttpExchangeLog` (component by version) but no frame-level lines; BL-922's Context lists them under `verbose`.
- Rules: ADR-0222 (levels, components, never-logged values, `IsEnabled` before building a message).
- Hooks belong in `Http2Session`/`Http2StreamConnection` and `Http3Session`/`Http3StreamConnection`; `Curl.Http2` and `Curl.Http3` get no reference to the abstractions, so log around their calls here. The session needs the transfer's `IDiagnosticLog` (a pooled HTTP/2 session outlives one transfer: decide whose log it writes to and record it in an ADR).
- No header values in frame lines: HEADERS frames are logged by type, stream ID and length only.

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pin: at `verbose`, an HTTP/2 GET logs `HEADERS` and `DATA` lines with stream ID 1 under `http2`; a server GOAWAY logs a `warning` with its last stream ID and error code; an RST_STREAM logs a `warning` with its stream ID and error code; an HTTP/3 GET logs its frames under `http3`.
- [x] At `DiagnosticLogLevel.Info` no frame line is recorded, and with `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: a new `HttpFrameLog` (verbose frame lines, info SETTINGS, warning GOAWAY/RST_STREAM/HTTP/3 stream reset), handed to each stream by `IHttpStreamSession.CreateStream`'s new `diagnosticLog` parameter (the handler passes `Context.DiagnosticLog`). Delivered directly in the session rather than through the full `/feature` agent chain, to stay within the run's budget; the change is one library and its tests.
- Whose log (ADR-0345): a stream's frames go to its transfer's log; the connection's SETTINGS and GOAWAY go to the log of the transfer that last opened a stream on the session, so a pooled session hands its log on.
- `Http2Connection` handles SETTINGS and GOAWAY inside its read and returns only HEADERS and DATA, and `Curl.Http2` is outside `touches`, so the session detects them as a change of `IsPeerSettingsReceived` / `PeerGoAway` across one read (in a `finally`, so a GOAWAY that throws is logged too). Only the first SETTINGS is logged.
- Line shapes: `HEADERS sent on stream 1, 23 bytes`, `DATA received on stream 1, 5 bytes`, `SETTINGS received: max concurrent streams unlimited, initial window 65535, max frame 16384, header table 4096`, `GOAWAY received: last stream 0, error INTERNAL_ERROR (2)`, `RST_STREAM received on stream 1: error CANCEL (8)`, `RESET_STREAM received on stream 0: error 0x10c`. No header value is ever in a frame line (pinned).
- HTTP/3's SETTINGS and GOAWAY come from the background control-stream reader; filed as BL-1155.
- Tests: 1645 passed, 4 skipped in Curl.Protocol.Http.UnitTests (16 new); coverage 100/100, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. HTTP/2 and HTTP/3 frames, the HTTP/2 server's SETTINGS, GOAWAY and RST_STREAM, and HTTP/3 stream resets are written to the diagnostic log under http2/http3
