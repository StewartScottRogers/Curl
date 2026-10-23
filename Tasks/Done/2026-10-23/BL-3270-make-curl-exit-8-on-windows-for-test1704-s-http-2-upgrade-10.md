---
id: BL-3270
title: Make curl exit 8 on Windows for test1704's HTTP/2 Upgrade 101 response and hold it on the Windows ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.Http2WindowsCases.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/HttpResponseHeadReaderTests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.H2cUpgrade.cs, Documentation/Planning/Decisions/ADR-0679-http2-status-line-over-http1-refused-off-windows.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3270 — Make curl exit 8 on Windows for test1704's HTTP/2 Upgrade 101 response and hold it on the Windows ratchet

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The task was filed with placeholder Goal, Context and criterion. Done as its title says: curl exits 8 on Windows for test1704 and the case is held on the Windows ratchet.
- Checked: On Windows, an `HTTP/2 101` status line over HTTP/1.x fails with exit 8 and `Version mismatch (from HTTP/1 to HTTP/2)`, as measured from an HTTP/2-built Windows curl, and a unit test in `Curl.Protocol.Http.UnitTests` pins it on every platform.
- Checked: test1704 is held on the conformance ratchet on Windows (`UpstreamConformanceTests.Http2WindowsCases.cs` removed).
- Checked: An ADR marked "Decided by Claude under Stewart's delegation" records the decision.
- Checked: Build clean, fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- Measured with Record-CurlExchange.ps1 against WinGet's curl 8.18.0 (LibreSSL, nghttp2 1.68.0, HTTP3) on Windows: test1704's reply, with or without `--http2`, exits 8, `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`, no stdout. `HTTP/3 200` there exits 8 `(from HTTP/1 to HTTP/3)` because that build has HTTP/3; Curl keeps exit 1 for HTTP/3, a no-HTTP/3 build's answer (ADR-0679), as Curl has no HTTP/3.
- Decision in ADR-0788 (amends ADR-0679): refusal on every platform; `RefusesHttp2StatusLines` removed with its Windows-only tests (`ReadAsync_Http2StatusLineOnWindows_ParsesAs200`, `ReadAsync_Http2InterimWhereNotRefused_TakesItAsAssumedHttp10Ok`); the handler-level test1704 test no longer excludes Windows.
- Widened touches to the reader, its tests, the H2c upgrade tests, UpstreamConformanceTests.cs (the call to the removed hold-back) and ADR-0679's status line; no task in Doing on origin/work/dark-factory names them.
- Platform-neutral: the refusal and its tests no longer depend on the OS, so CI on Linux and macOS is unaffected.
- Fast tests all green on Windows; quality run 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. test1704 exits 8 with Version mismatch on Windows too and is held on the Windows ratchet
