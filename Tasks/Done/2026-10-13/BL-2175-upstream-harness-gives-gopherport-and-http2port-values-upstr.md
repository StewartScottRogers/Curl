---
id: BL-2175
title: Upstream harness gives %GOPHERPORT and %HTTP2PORT values (upstream test1609, test5028, test2407)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2175 — Upstream harness gives %GOPHERPORT and %HTTP2PORT values (upstream test1609, test5028, test2407)

## Goal

The upstream harness substitutes `%GOPHERPORT` and `%HTTP2PORT` and routes them to an in-memory server, so upstream test1609, test5028 (gopher) and test2407 (HTTP/2) run instead of skipping.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13: the three cases are skipped with "the harness has no value for %GOPHERPORT" / "%HTTP2PORT". test2407 is also a `<client><tool>` libtest, so it may still skip for that reason afterwards; say so under Notes. Harness: `Curl.Conformance.UnitLibrary` (ADR-0013, ADR-0460).

## Acceptance criteria

- [x] test1609 and test5028 no longer skip for a missing port variable; each one that passes is on `PassingUpstreamCases.txt`, and each that fails has its cause filed as a task.
- [x] `%HTTP2PORT` has a value, and test2407's remaining skip reason (if any) is recorded under Notes.
- [x] Harness unit tests cover the new variables; `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (decided in the run, no ADR needed: it follows ADR-0013 and the existing stand-in pattern): `%GOPHERPORT` is 9008 and routes to a new in-memory `GopherServerConnector` / `GopherServerConnection`, emulating `sws --gopher` (request ends at the first CRLF; test number after the last slash, past non-digits, picks `<data>` or `<dataN>` above 10000; reply sent as written, then close). It sits outermost of the runner's chain (inside the SSH stand-in) and its selector bytes join the merged `<verify><protocol>` dump. Screening lets `gopher` cases run. `%HTTP2PORT` is 9009; no nghttpx/HTTP/2 stand-in stands behind it (screening still skips the `http/2` server), so a connection there reaches the sws emulation. Ports 9008 and 9009 were the next free ones (9003-9007 are TFTP/SSH, passive, active and FTPS).
- Measured 2026-10-13 after the change: test1200, test1201 and test1202 (gopher, previously skipped for `%GOPHERPORT`) pass and are on `PassingUpstreamCases.txt`. test1609 runs and fails on a Curl difference: Curl sends `/sel\r\n` and exits 0, curl 8.22.0 rejects the decoded CR LF with exit 3 and sends nothing - filed as BL-2185. test5028 now skips for `Curl lacks the feature Debug` (it also needs `CURL_SMALLSENDS`, a debug-build switch), not for a missing port.
- test2407 still skips: `the harness does not act on <client><tool>` (it is libtest lib2405); were it not a `<tool>` case it would skip next for `the harness does not emulate the http/2 server`. 1700, 1702, 358 and 359 skip for the `http/2` server too.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100% line, 100% branch, 0 failing members. `dotnet build` clean; fast tests green (Conformance 2731 passed, 0 failed).

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. %GOPHERPORT reaches an in-memory sws --gopher stand-in and %HTTP2PORT has a value; test1200-1202 pass, test1609 runs (fix filed as BL-2185)
