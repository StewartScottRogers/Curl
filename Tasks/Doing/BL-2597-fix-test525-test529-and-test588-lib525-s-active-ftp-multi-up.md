---
id: BL-2597
title: Fix test525, test529 and test588: lib525's active-FTP multi upload hangs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2597 — Fix test525, test529 and test588: lib525's active-FTP multi upload hangs

## Goal

test525, test529 and test588 pass through the upstream case runner and are on the ratchet.

## Context

Found by BL-2596, which ported `lib525.c` as `Lib525` (`Curl.Conformance.LibTest.UnitLibrary`). All three
cases run lib525: an FTP upload with `CURLOPT_UPLOAD`, active mode (`CURLOPT_FTPPORT "-"`), `CURLOPT_READDATA`
an open file and `CURLOPT_INFILESIZE_LARGE`, driven by `curl_multi_perform` and a one-second `select()`
(`curl_multi_wait`) loop. On Windows each one fails with "curl did not finish within 20 seconds"; test531
(same program, upload refused by the server) passes, so the hang is in the active-mode data connection of a
multi-driven upload. Reproduce with `CURL_UPSTREAM_CASE=525 dotnet test Curl.Conformance.UnitTests --filter
"Name=NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust"`. If the cause is outside the FTP library,
widen `touches` by the lane rules.

## Acceptance criteria

- [ ] test525, test529 and test588 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 326c7b1a fd6bd9d4 3d836d5e.
