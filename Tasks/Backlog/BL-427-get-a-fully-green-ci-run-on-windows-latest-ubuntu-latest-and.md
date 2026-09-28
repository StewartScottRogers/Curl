---
id: BL-427
title: Get a fully green CI run on windows-latest, ubuntu-latest and macos-latest
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-418, BL-419, BL-420, BL-421, BL-422, BL-423, BL-424, BL-425, BL-426, BL-472, BL-473]
touches: []
requirement: none
created: 2026-09-27
completed:
---
# BL-427 — Get a fully green CI run on windows-latest, ubuntu-latest and macos-latest

## Goal

A run of the `CI` workflow (`.github/workflows/ci.yml`) on `work/dark-factory` concludes
`success` in all three jobs: `Build and test (windows-latest)`, `Build and test (ubuntu-latest)`
and `Build and test (macos-latest)`, so auto-merge to `master` can be gated on all three.

## Context

CI had never passed on Linux or macOS. CI run 36344057083 failed 369 fast tests on ubuntu-latest
and 371 on macos-latest while windows-latest passed, because dark factory lanes build and test only
on Windows. The failures were filed by root cause:

- Windows drive-letter `file://` URLs in test fixtures, which curl's non-Windows builds reject
  (ADR-0010; curl `lib/urlapi.c` `CURLUE_BAD_FILE_URL`), 367 results on each platform:
  BL-418 (`FileUrlPathTests`, 67), BL-419 (`FileProtocolHandlerTests`, 145), BL-420 (four smaller
  `FileProtocolHandler*Tests`, 50), BL-421 (six `CurlCommandRunner*Tests` sharing `SourceUrl`, 70),
  BL-422 (six more `Curl.Console.UnitTests` classes, 31), BL-423 (`Curl.Core.UnitTests`, 4, two of
  which are real platform differences production already gets right).
- A test that assumes the Windows build's missing-file reporting for `-z`: BL-424 (`Curl.Cli.UnitTests`, 1).
- A certificate fixture only Windows' loader accepts: BL-425 (`Curl.Output.UnitTests`, 1 on each platform).
- A brainpool key macOS cannot generate: BL-426 (`Curl.Output.UnitTests`, 2 on macOS only).

Tasks finished by other lanes while these ran may have added new tests with the same Windows
assumptions, so this task checks the whole run, not only the tests listed above.

Steps: find the latest `CI` run on `work/dark-factory` (`gh run list --workflow CI --branch
work/dark-factory --limit 1`); if a job failed, read `gh run view <run-id> --log-failed`, group any
remaining failures by root cause, and have `task-planner` file one High task per cause (assignee
Claude, exact `touches`), add them to this task's `depends-on`, and move this task to `Blocked`
naming them. When every job is green, record the run ID in `## Log` and finish.

## Acceptance criteria

- [ ] `gh run view <run-id> --json conclusion,jobs` for a `CI` run on `work/dark-factory` whose
      head commit contains BL-418 to BL-426 shows `conclusion: success` and every one of the three
      `Build and test` jobs `success`; the run ID is recorded in `## Log`.
- [ ] That run's `Fast tests` step reports `Failed: 0` for every test assembly on all three jobs.

## Notes

- 2026-09-27: CI run 36376508151 (head 46db192, contains BL-418 to BL-426) passed on
  windows-latest and failed 4 fast tests on ubuntu-latest and macos-latest, two root causes:
  the three `RunAsync_TimeCondNotADate*` tests in `Curl.Console.UnitTests` pin only the Windows
  build's `-z` warnings (off Windows curl adds `Failed to get filetime: No such file or
  directory` first) -> BL-472; `ReportTlsData_WritesNothing` in `Curl.Output.UnitTests` takes
  the platform-default TLS backend, which is OpenSSL off Windows -> BL-473. Filed rather than
  fixed here so this task stays a check, as its Context asks; moved to Backlog (not Blocked)
  because only other work stands in the way.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Waiting on BL-472 and BL-473 (last Linux/macOS CI failures in run 36376508151)
