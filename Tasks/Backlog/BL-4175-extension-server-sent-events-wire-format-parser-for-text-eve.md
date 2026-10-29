---
id: BL-4175
title: Extension: Server-Sent Events wire-format parser for text/event-stream
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ServerSentEvents.UnitLibrary, Curl.Extension.ServerSentEvents.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4175 — Extension: Server-Sent Events wire-format parser for text/event-stream

## Goal

`Curl.Extension.ServerSentEvents.UnitLibrary` exists with a parser that turns `text/event-stream` bytes into events (`event`, `data`, `id`, `retry`) exactly as WHATWG HTML "Server-sent events" §9.2.6 "Interpreting an event stream" says.

## Context

Split from BL-4110 for size (its Notes ask for "wire format, then the client flow, then the options"). Extension rules: ADR-0999, ADR-1003 (projects join by wildcard reference and one single-line `<Folder Name="/Extensions/ServerSentEvents/">` entry in `Curl.slnx` under the marker comment). Use the `new-project` skill. References only `Curl.Extension.Abstractions` and `Curl.Protocol.Abstractions` if needed. The parser is incremental (bytes arrive in arbitrary chunks): UTF-8 decode with BOM stripped once, lines ended by CRLF, LF or CR, comment lines (`:`), field/value split at the first colon with one leading space removed, `data` joined by LF with the trailing LF dropped, `id` containing NUL ignored, `retry` only ASCII digits, an empty line dispatches (an event with empty data is not dispatched, the last event ID buffer persists). BL-4110 adds the option, hook, reconnection and registration on top.

## Acceptance criteria

- [ ] `Curl.Extension.ServerSentEvents.UnitLibrary` and `Curl.Extension.ServerSentEvents.UnitTests` exist in `/Extensions/ServerSentEvents/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] Its parser yields events, the last event ID and the reconnection time from `text/event-stream` bytes fed in arbitrary chunks, per §9.2.6, with unit tests citing the section for each rule above (including the specification's own examples).
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
