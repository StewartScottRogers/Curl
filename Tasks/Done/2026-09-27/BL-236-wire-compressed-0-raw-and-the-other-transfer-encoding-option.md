---
id: BL-236
title: Wire --compressed, -0, --raw and the other transfer-encoding options in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177, BL-180, BL-191, BL-231, BL-315]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-236 — Wire --compressed, -0, --raw and the other transfer-encoding options in Curl.Console

## Goal

`--compressed`, `-0`, `--http1.1`, `--raw`, `--tr-encoding` and `--ignore-content-length` reach `HttpRequestOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-154 ADR fixes the Accept-Encoding value.

## Acceptance criteria

- [x] A Console test per option shows the request bytes or output BL-177/BL-180 pinned.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- 2026-09-26: BL-180 left `--tr-encoding` to BL-315, which adds its `HttpRequestOptions` member; this task now waits on it too.

- 2026-09-27 (lane 2, unattended). **Delivered directly**, not through the architect and implementer
  subagents: the change is six property copies in `HttpRequestOptionsMapping.FromCommandLine`, the
  parser and handler already existed. `Version = options.HttpVersion ?? Http11` (the parser's
  `HttpVersion` is null when neither `-0` nor `--http1.1` was given; Http11 is the handler's default).
  No ADR: nothing here is a design choice, only wiring of measured behaviour.
- Tests: `Curl.Console.UnitTests/CurlCommandRunnerTransferEncodingTests.cs`, eight tests through the
  runner and the real `HttpProtocolHandler` over `ScriptedConnector`, pinning the bytes BL-177
  (`--compressed` Accept-Encoding and gzip decode, and the undecoded body without it), BL-180 (`-0`,
  `--http1.0`, `--raw` chunked body, `--ignore-content-length` read to close) and BL-315
  (`--tr-encoding` `TE: gzip` / `Connection: TE` and gzip transfer coding decoded) measured on
  curl 8.21.0; `-0 --http1.1` shows the last version option wins.
- Gates: `dotnet build -warnaserror` clean; fast run green (Curl.Console.UnitTests 637 passed, every
  other project 0 failed). `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration`:
  100% line, 100% branch, 0 failing members, worst CRAP 10. Without `-IncludeIntegration` it flags
  only `DiskWriteOutFileOpener.TryOpen`, whose tests are Integration, as before (BL-240, BL-328).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --compressed, -0/--http1.0, --http1.1, --raw, --tr-encoding and --ignore-content-length now reach HttpRequestOptions from the command line
