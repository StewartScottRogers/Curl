---
id: BL-2636
title: Make upstream case 753 run: the runner gives a libtest case the Debug feature it needs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2636 — Make upstream case 753 run: the runner gives a libtest case the Debug feature it needs

## Goal

Upstream case 753 runs through the runner and, when it passes, holds the ratchet.

## Context

Found by BL-2536, which ported lib753 (`Curl.Conformance.LibTest.UnitLibrary/Lib753.cs`) and unit tested it over a fake engine. test753 is skipped: `test753 is skipped: Curl lacks the feature Debug`. The case needs `<features>Debug ftp</features>` and sets `CURL_FTP_PWD_STOP=1`, a debug-build switch that stops libcurl's FTP state machine after `PWD`; the program cleans EASY1 up without removing it from the multi handle and checks, through the `<protocol>` lines, that EASY2 does not reuse its connection (USER/PASS twice, then PWD, EPSV, TYPE I, SIZE 753, QUIT). In the first pass decide (ADR if it is a behaviour choice) whether Curl's FTP engine honours `CURL_FTP_PWD_STOP` and whether the runner may then grant the Debug feature for this case, find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] test753 is run (not skipped) by `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` with `CURL_UPSTREAM_CASE=753`; if it passes on Windows it is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, else it is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
