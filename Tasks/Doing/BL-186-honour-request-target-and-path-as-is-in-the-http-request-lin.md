---
id: BL-186
title: Honour --request-target and --path-as-is in the HTTP request line
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-172, BL-010, BL-293, BL-294]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-186 — Honour --request-target and --path-as-is in the HTTP request line

## Goal

`RequestTarget` replaces the request-line target verbatim, and under `--path-as-is` dot segments reach the request line unsquashed.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H18. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `System.Uri` squashes dot segments; BL-010 decides the URL representation that makes `--path-as-is` possible.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `--request-target '*'` with `-X OPTIONS` and `--path-as-is` with `/a/../b` each match curl 8.21.0's request line (measured).
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H18 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Depends on BL-293 and BL-294 as well: ADR-0010 accepted `CurlUrl`, which keeps dot segments under path-as-is (BL-010).
- 2026-09-27: Backlog -> Doing.
