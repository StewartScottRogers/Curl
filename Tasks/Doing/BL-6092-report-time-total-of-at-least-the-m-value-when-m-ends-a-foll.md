---
id: BL-6092
title: Report %{time_total} of at least the -m value when -m ends a followed redirect hop
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6082]
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerMaxTimeTotalTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6092 — Report %{time_total} of at least the -m value when -m ends a followed redirect hop

## Goal

Under `-L -m`, a transfer that `-m` ends on a followed redirect hop reports a `%{time_total}` of at least the `-m` value, as curl does.

## Context

Follow-up of BL-6082 (ADR-1470, Consequences). `RedirectFollower` measures the merged total from the first hop's timings, whose start is the first handler's, a few milliseconds after the `-m` watchdog's (`ITransferContext.OperationStarted`), so the total can be just under `-m`. Seen by reading the code, not yet reproduced: reproduce with a loopback server that answers a 302 to a listener that never answers, `-s -L -m 2 -w "%{time_total}"`, and measure real curl with `Record-CurlExchange.ps1`. If the fix needs `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs`, add it to `touches`.

## Acceptance criteria

- [ ] The `-L -m 2` repro prints `t` of at least 2.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected time (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing. Criteria at claim: b1be4df0 8a7549dc 3d836d5e.
