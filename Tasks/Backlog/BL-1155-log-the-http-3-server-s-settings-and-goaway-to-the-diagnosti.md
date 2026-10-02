---
id: BL-1155
title: Log the HTTP/3 server's SETTINGS and GOAWAY to the diagnostic log in Curl.Protocol.Http
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1073]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1155 — Log the HTTP/3 server's SETTINGS and GOAWAY to the diagnostic log in Curl.Protocol.Http

## Goal

`Curl.Protocol.Http.UnitLibrary` writes the HTTP/3 server's SETTINGS at `info` and its GOAWAY at `warning` (with the stream ID it carries) to the diagnostic log under component `http3`, as BL-1073 does for HTTP/2.

## Context

- Follow-up of BL-1073 (ADR-0345 point 5): HTTP/2 logs the server's SETTINGS and GOAWAY through `HttpFrameLog`, but HTTP/3's arrive on the control stream, read in the background by `Http3Session.ReadPeerStreamsAsync` with no transfer at hand.
- ADR-0345 point 3 gives the rule: the connection's own frames go to the log of the transfer that last opened a stream on the session. `Http3Session.OpenRequestStreamAsync` is where that transfer is known; `Http3ControlStreamReader` exposes `PeerSettings` and `GoawayStreamId`.
- Rules: ADR-0222 (levels, components, `IsEnabled` before building a message).

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pin: an HTTP/3 GET whose server control stream sends SETTINGS logs an `info` line `SETTINGS received: ...` under `http3`; a server GOAWAY logs a `warning` naming its stream ID.
- [ ] With `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
