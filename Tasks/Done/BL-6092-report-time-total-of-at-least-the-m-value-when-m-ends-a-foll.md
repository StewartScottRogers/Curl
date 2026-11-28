---
id: BL-6092
title: Report %{time_total} of at least the -m value when -m ends a followed redirect hop
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6082]
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitLibrary/CLAUDE.md, Curl.Core.UnitTests/RedirectFollowerMaxTimeTotalTests.cs]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-6092 — Report %{time_total} of at least the -m value when -m ends a followed redirect hop

## Goal

Under `-L -m`, a transfer that `-m` ends on a followed redirect hop reports a `%{time_total}` of at least the `-m` value, as curl does.

## Context

Follow-up of BL-6082 (ADR-1470, Consequences). `RedirectFollower` measures the merged total from the first hop's timings, whose start is the first handler's, a few milliseconds after the `-m` watchdog's (`ITransferContext.OperationStarted`), so the total can be just under `-m`. Seen by reading the code, not yet reproduced: reproduce with a loopback server that answers a 302 to a listener that never answers, `-s -L -m 2 -w "%{time_total}"`, and measure real curl with `Record-CurlExchange.ps1`. If the fix needs `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs`, add it to `touches`.

## Acceptance criteria

- [x] The `-L -m 2` repro prints `t` of at least 2.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected time (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced 2026-11-28 with `Record-CurlExchange.ps1` (a 302 to a listener that never answers, `-sS -L -m 2`): real curl `t=2.259170`, Curl `t=1.965191`. With the 302 held 1.2 s: real curl `t=2.282599`. After the fix Curl printed `t=2.017560` to `2.028001`.
- Fixed in `RedirectFollower` (ADR-1472): a followed hop that ends with exit 28 counts the merged timings from the context's `OperationStarted`. `EndPointReportingProtocolHandler.cs` was not needed.
- Added `Curl.Core.UnitLibrary/CLAUDE.md` to `touches`: one clause so its description of the merged timings stays true; no task in Doing names it.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 0 failing members. `dotnet build` 0 warnings; fast tests passed.
- Seen while measuring, filed as BL-6095: Curl prints `%{http_code}` `000` where curl prints `302` in the same repro.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing. Criteria at claim: b1be4df0 8a7549dc 3d836d5e.
- 2026-11-28: Doing -> Done. A followed redirect hop that -m ends counts the merged time_total from -m's start (ADR-1472); follow-up BL-6095 filed for http_code.
