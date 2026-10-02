---
id: BL-1169
title: Write the --trace-config quic lines from the QUIC transport
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1169 — Write the --trace-config quic lines from the QUIC transport

## Goal

Under `-v --trace-config quic` (and `all`) Curl writes the `* [QUIC] ...` lines curl 8.21.0 writes for an HTTP/3 transfer, from `Curl.Quic.UnitLibrary`.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on and hands the QUIC transport an `ITransferEvents` sink.
- Not measured yet: the reference Schannel build has no QUIC. Measure on an OpenSSL build of curl 8.21.0 with HTTP/3 against a local HTTP/3 server, run with `Record-CurlExchange.ps1 -NoServer`, and record the lines in Notes. Measure too which of `protocol`, `network` and `-vvvv` turn `quic` on (curl's manual puts it under `network`).

## Acceptance criteria

- [ ] The `[QUIC]` lines of an HTTP/3 GET under `-v --trace-config quic` are measured and recorded in Notes, with which umbrella names turn them on.
- [ ] Tests pin them; the measured umbrella names write the same; `-v` alone, another component and `quic` without `-v` write none.
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
