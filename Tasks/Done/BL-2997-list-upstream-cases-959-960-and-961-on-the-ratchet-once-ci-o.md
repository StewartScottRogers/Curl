---
id: BL-2997
title: List upstream cases 959, 960 and 961 on the ratchet once CI on Linux and macOS shows them passing under the engine feature mask, or record their first difference
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-22
---
# BL-2997 — List upstream cases 959, 960 and 961 on the ratchet once CI on Linux and macOS shows them passing under the engine feature mask, or record their first difference

## Goal

Upstream cases 959, 960 and 961 (SMTP mailbox hosts in UTF-8 without IDN, `!IDN` `!win32`) are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` because a CI run measured them passing on Linux and macOS, or their first difference on each platform is in this task's Notes and filed as a fix task.

## Context

Split from BL-2996, whose third criterion this is. BL-2983 made SMTP send a mailbox host as written under the engine feature mask's `Idn` bit (`SmtpMailbox.Bare`/`Bracketed`'s `hostAsWritten`), and BL-2856 made the harness run `!IDN` cases under that mask (`UpstreamConformanceTests.MissingFeatureCases.cs`). On Windows all three are `SkippedOnThisPlatform` (`!win32`), so a lane cannot run them: only CI's Linux and macOS jobs can. Listing a case without a measurement risks a red CI (AF-0226), so measure first.

How to measure without listing: find a finished `CI` run on `work/dark-factory` at or after commit e75f7d1bd (BL-2856) and read its Linux and macOS test logs for `test959`, `test960` and `test961` (`gh run view <id> --log | Select-String 'test9(59|60|61)'`). If the log does not show per-case outcomes for unlisted cases, run them through a `workflow_dispatch` or a CI run of a commit that sets `CURL_UPSTREAM_CASE` for one job, whichever the workflow allows, without changing guard files (`.github/workflows/ci.yml` is a guard file and changes only through the `audit` branch).

## Acceptance criteria

- [x] 959, 960 and 961 are on `PassingUpstreamCases.txt` once a CI run shows them passing on Linux and macOS, or their first difference on each platform is in Notes with a fix task filed for it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Conformance.UnitTests` on Windows.

## Notes

- Interactive only (lane 6, 2026-10-21): CI run 39688343648 (success, after e75f7d1bd) shows test959, test960 and test961 as `Skipped` on Linux, macOS and Windows alike - unlisted cases are skipped by the ratchet, so the log carries no outcome for them. The only measuring routes are a CI job that sets `CURL_UPSTREAM_CASE` (needs `.github/workflows/ci.yml`, a guard file) or the gap platforms run (`Gap/`, which a lane may not read). Listing them unmeasured risks a red CI (AF-0226).
- Remove `lane: no` once a Linux and macOS run has measured 959, 960 and 961 (an interactive session runs them with `CURL_UPSTREAM_CASE` on the `audit` branch's workflow, or reads the gap-platforms behaviour measurement) and the outcomes are pasted here.

- Measured 2026-10-22 (interactive): branch probe/bl-2997 (2c386a9b6) listed 959, 960 and 961 on the ratchet; CI run 39816644139 passed on Linux and macOS. Curl.Conformance.UnitTests went from Passed 4105 / Skipped 63 (Linux) and 4104 / 64 (macOS) in run 39806140554 on d0796ba22 to 4108 / 60 and 4107 / 61, with no other code change between, so all three ran and passed on both. On Windows they stay skipped (!win32), allowed by ADR-0488. The gap office measures them as excluded (reference-lacks:codeset-utf8), so only the ratchet can measure them.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 49f7fc8c 55566d96.
- 2026-10-21: Doing -> Backlog. Interactive only: measuring 959-961 on Linux and macOS needs the CI workflow file or the gap folder, both off limits to a lane; lane: no set
- 2026-10-22: Backlog -> Doing. Criteria at claim: 49f7fc8c 55566d96.
- 2026-10-22: Doing -> Done. 959, 960 and 961 listed: CI run 39816644139 ran and passed all three on Linux and macOS.
