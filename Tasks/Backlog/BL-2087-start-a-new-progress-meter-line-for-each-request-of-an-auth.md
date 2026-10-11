---
id: BL-2087
title: Start a new progress-meter line for each request of an auth-retried transfer (upstream test1075, test1072)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2087 — Start a new progress-meter line for each request of an auth-retried transfer (upstream test1075, test1072)

## Goal

Curl draws a new progress-meter line for each request of an authentication-retried transfer, as curl 8.21.0 does, so the progress output of upstream test1075 and test1072 matches.

## Context

In curl each request of a retried transfer (a 401 or 407 followed by the authenticated request) starts its own progress-meter line; Curl draws one meter for the whole transfer (Curl.Console/ProgressMeterLines.cs, ParallelProgressMeter.cs, TransferProgressRecorder.cs). Read 8.21.0 `tests/data/test1075` and `test1072` and measure real curl with `Record-CurlExchange.ps1` to pin the exact stderr bytes (header line, and what the first line shows when its request ends in a 401) before changing anything. Find where the retry reaches the console's progress recorder and start a fresh line there.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2074 (test1075) and BL-2075 (test1072). Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] New MSTests in Curl.Console.UnitTests (no network, platform-neutral) show a transfer whose first request is answered 401 and retried produces two meter lines with the stderr bytes real curl 8.21.0 gives, and a transfer without a retry still produces one.
- [ ] `--ai-help` needs no change (no option is added or changed); the task Log says so.
- [ ] Add test1075 and, once the rest of its output matches, test1072 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
