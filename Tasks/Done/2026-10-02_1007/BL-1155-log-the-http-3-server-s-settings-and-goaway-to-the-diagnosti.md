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
completed: 2026-10-02
---
# BL-1155 — Log the HTTP/3 server's SETTINGS and GOAWAY to the diagnostic log in Curl.Protocol.Http

## Goal

`Curl.Protocol.Http.UnitLibrary` writes the HTTP/3 server's SETTINGS at `info` and its GOAWAY at `warning` (with the stream ID it carries) to the diagnostic log under component `http3`, as BL-1073 does for HTTP/2.

## Context

- Follow-up of BL-1073 (ADR-0345 point 5): HTTP/2 logs the server's SETTINGS and GOAWAY through `HttpFrameLog`, but HTTP/3's arrive on the control stream, read in the background by `Http3Session.ReadPeerStreamsAsync` with no transfer at hand.
- ADR-0345 point 3 gives the rule: the connection's own frames go to the log of the transfer that last opened a stream on the session. `Http3Session.OpenRequestStreamAsync` is where that transfer is known; `Http3ControlStreamReader` exposes `PeerSettings` and `GoawayStreamId`.
- Rules: ADR-0222 (levels, components, `IsEnabled` before building a message).

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pin: an HTTP/3 GET whose server control stream sends SETTINGS logs an `info` line `SETTINGS received: ...` under `http3`; a server GOAWAY logs a `warning` naming its stream ID.
- [x] With `NoDiagnosticLog.Instance` every existing test passes unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `Http3Session.OpenRequestStreamAsync` now takes the opening transfer's `HttpFrameLog` (ADR-0345 point 3) and makes it the connection log; `Http3StreamConnection` passes its own. The control-stream reader hands each frame to `LogConnectionFrame`, which writes SETTINGS (`info`) and GOAWAY (`warning`) through `HttpFrameLog.Http3SettingsReceived` / `Http3GoawayReceived` under `http3`; other control frames are not logged.
- Choice: the server's SETTINGS usually arrive before any transfer has opened a stream (the fakes deliver them in the session's constructor), so frames that arrive with no transfer log yet are held and written to the first transfer that opens a stream, rather than dropped.
- Choice: line format `SETTINGS received: MAX_FIELD_SECTION_SIZE 100, 0x21 7` - each setting in the order sent, by its RFC name, an unknown one by hex identifier, `none` for an empty frame; `GOAWAY received: stream 4`. These are Curl's own log lines (ADR-0222), not curl output, so there is nothing to measure against real curl.
- Setting names are a dictionary lookup, not a `switch`: the compiler's range check on a constant `switch` left one branch the coverage collector could not reach.
- The five existing `Http3SessionTests` calls of `OpenRequestStreamAsync` now pass `HttpFrameLog.Silent` for the new parameter; their assertions are unchanged.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Curl.Protocol.Http logs the HTTP/3 server's SETTINGS at info and GOAWAY at warning under http3 to the transfer that last opened a stream
