---
id: BL-2623
title: Make CURLOPT_CONNECT_ONLY through a multi handle connect in upstream cases 597 and 660
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2623 — Make CURLOPT_CONNECT_ONLY through a multi handle connect in upstream cases 597 and 660

## Goal

Upstream cases 597 (FTP) and 660 (IMAP), both lib597, pass: a `CURLOPT_CONNECT_ONLY` transfer through a multi handle connects and logs in, then completes with a done message.

## Context

Found by BL-2533: `Lib597` is unit tested and matches lib597.c, but nothing reaches the server: 597 `<verify><protocol> differs at byte 0 (line 1): expected "USER anonymous\r\n", got the end`; 660 `expected "A001 CAPABILITY\r\n", got the end`. lib556 shows connect-only works through `curl_easy_perform`; the multi path does not. In the first pass find the Libcurl multi or engine project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
