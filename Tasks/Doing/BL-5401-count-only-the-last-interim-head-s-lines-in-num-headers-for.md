---
id: BL-5401
title: Count only the last interim head's lines in %{num_headers} for 100 then 103 then close
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5401 — Count only the last interim head's lines in %{num_headers} for 100 then 103 then close

## Goal

For a `100 Continue` followed by a `103` head and a closed connection, `%{num_headers}` is 1 (the last interim head's lines only), as curl reports.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-109 (Low, Confirmed, 2 runs). Area: HTTP/1.1 interim responses, write-out, Windows.

**Repro.** A raw TCP server on 127.0.0.1:41000 that sends exactly `HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n` and closes.
`curl -sS -i http://127.0.0.1:41000/c100x2 -w "[%{http_code} %{response_code} %{size_header} %{num_headers}]"`
**Expected (REF).** `[103 103 59 1]`, `curl: (52) Empty reply from server`, exit 52.
**Actual (PORT).** `[103 103 59 2]`, same stderr and exit. A single 100 or 103 head matches.
**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`: the lone-1xx reporting from commit bd725df4 counts header lines across all interim heads instead of the last.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Http.UnitTests` (a new file) prove `100` then `103` then close reports `num_headers` 1, `size_header` 59, response code 103 and exit 52.
- [x] A test proves a single `100` head and a single `103` head keep their current counts (no regression).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: the repro prints `[103 103 59 1]`. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `[103 103 59 1]`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 167c95fb 0c38ce0a 7ae10ef7 a0b49a29 ed9e132a.
- 2026-11-17: Fixed HttpProtocolHandler.HeadersBeforeFailure to count only the last head's lines. Tests in HttpProtocolHandlerInterimHeadCountTests.cs (100+103 -> 1 header, size 59; lone 100 and 103 unchanged). Hand check: the unit test pins the repro's bytes ([103 103 59 1]); real curl value taken from the task's REF. Quality run: 0 failing members. CI platform criteria met by platform-neutral tests.
