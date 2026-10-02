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
completed:
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

- [ ] `Curl.Protocol.Http.UnitTests` pin: at `verbose`, an HTTP/2 GET logs `HEADERS` and `DATA` lines with stream ID 1 under `http2`; a server GOAWAY logs a `warning` with its last stream ID and error code; an RST_STREAM logs a `warning` with its stream ID and error code; an HTTP/3 GET logs its frames under `http3`.
- [ ] At `DiagnosticLogLevel.Info` no frame line is recorded, and with `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
