---
id: BL-2631
title: Make upstream cases 670 to 673 pass: a read callback's CURL_READFUNC_PAUSE resumes after curl_easy_pause
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2631 — Make upstream cases 670 to 673 pass: a read callback's CURL_READFUNC_PAUSE resumes after curl_easy_pause

## Goal

Upstream cases 670, 671, 672 and 673 pass through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. All four (lib670: MIME or form part of length 2 whose read callback gives `A`, returns `CURL_READFUNC_PAUSE`, then `B` after `curl_easy_pause(CURLPAUSE_CONT)` 5 seconds later): `<verify><protocol> differs at byte 233 (line 10): expected "AB\r\n", got the end` after `A` - the paused upload never resumes, both on the multi (670, 672) and easy (671, 673, unpaused from the xferinfo callback) interfaces.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
