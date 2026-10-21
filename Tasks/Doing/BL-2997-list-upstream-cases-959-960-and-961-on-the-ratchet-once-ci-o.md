---
id: BL-2997
title: List upstream cases 959, 960 and 961 on the ratchet once CI on Linux and macOS shows them passing under the engine feature mask, or record their first difference
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2997 — List upstream cases 959, 960 and 961 on the ratchet once CI on Linux and macOS shows them passing under the engine feature mask, or record their first difference

## Goal

Upstream cases 959, 960 and 961 (SMTP mailbox hosts in UTF-8 without IDN, `!IDN` `!win32`) are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` because a CI run measured them passing on Linux and macOS, or their first difference on each platform is in this task's Notes and filed as a fix task.

## Context

Split from BL-2996, whose third criterion this is. BL-2983 made SMTP send a mailbox host as written under the engine feature mask's `Idn` bit (`SmtpMailbox.Bare`/`Bracketed`'s `hostAsWritten`), and BL-2856 made the harness run `!IDN` cases under that mask (`UpstreamConformanceTests.MissingFeatureCases.cs`). On Windows all three are `SkippedOnThisPlatform` (`!win32`), so a lane cannot run them: only CI's Linux and macOS jobs can. Listing a case without a measurement risks a red CI (AF-0226), so measure first.

How to measure without listing: find a finished `CI` run on `work/dark-factory` at or after commit e75f7d1bd (BL-2856) and read its Linux and macOS test logs for `test959`, `test960` and `test961` (`gh run view <id> --log | Select-String 'test9(59|60|61)'`). If the log does not show per-case outcomes for unlisted cases, run them through a `workflow_dispatch` or a CI run of a commit that sets `CURL_UPSTREAM_CASE` for one job, whichever the workflow allows, without changing guard files (`.github/workflows/ci.yml` is a guard file and changes only through the `audit` branch).

## Acceptance criteria

- [ ] 959, 960 and 961 are on `PassingUpstreamCases.txt` once a CI run shows them passing on Linux and macOS, or their first difference on each platform is in Notes with a fix task filed for it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Conformance.UnitTests` on Windows.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 49f7fc8c 55566d96.
