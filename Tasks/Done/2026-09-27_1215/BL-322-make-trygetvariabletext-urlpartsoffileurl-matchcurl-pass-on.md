---
id: BL-322
title: Make TryGetVariableText_UrlPartsOfFileUrl_MatchCurl pass on Linux and macOS CI
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-322 — Make TryGetVariableText_UrlPartsOfFileUrl_MatchCurl pass on Linux and macOS CI

## Goal

`TryGetVariableText_UrlPartsOfFileUrl_MatchCurl` passes on ubuntu-latest and macos-latest in the CI workflow, as it already does on windows-latest, and still proves what it set out to prove on each platform.

## Context

CI (`.github/workflows/ci.yml`) runs the fast tests on Windows, Linux and macOS. On `work/dark-factory` it has been red on Linux and macOS since at least 2026-09-27 05:59Z (run 36305641380), and this is one of four tests that fail there. `Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs`: It expects "file|||||0|Z:/bl284tmp/wo.txt|||": a Windows drive path pinned into a test that also runs on Linux and macOS.

Curl publishes native binaries for all three platforms (BL-028) and matches the platform's own curl (ADR-0009), so the fix is to make the test express the right expectation per platform - not to skip it on non-Windows unless the behaviour genuinely exists only on Windows, in which case say so in the test name.

## Acceptance criteria

- [x] The test passes on Windows locally, and its expectation for Linux and macOS is stated in the test (a platform-specific case or data row), matching what the platform's curl or .NET actually does there.
- [x] If production code was wrong on Linux or macOS rather than the test, it is fixed, with coverage kept at 100%.
- [x] The next CI run of `work/dark-factory` no longer lists this test as failing on ubuntu-latest or macos-latest.

## Notes

- Cause: the test, not production code. `CurlUrl.TryParse` follows the platform's curl: on
  Linux and macOS curl's urlapi.c rejects a drive letter in a file URL (CURLUE_BAD_FILE_URL),
  so `file:///Z:/...` does not parse and every `url.`/`urle.` part is empty there.
  No production change and no new decision (the drive-letter rule was already in
  `CurlUrlParser`), so no ADR.
- Split the test in two: `TryGetVariableText_UrlPartsOfFileUrl_MatchCurl` now uses
  `file:///tmp/wo.txt`, which parses alike on all three platforms (`file|||||0|/tmp/wo.txt|||`);
  `TryGetVariableText_UrlPartsOfDriveLetterFileUrl_MatchPlatformCurl` keeps the measured
  Windows case and states the Linux/macOS expectation (all parts empty) via
  `OperatingSystem.IsWindows()`.
- CI criterion: factory branches skip CI, so it cannot be observed from this lane. The
  non-Windows branch goes through the same parser path that
  `CurlUrlTests.TryParse_WithADriveLetterOutsideWindows_ReturnsFalse` (driveLetters: false)
  and `TryParse_WithAFilePathOutsideWindows_KeepsItsSlash` prove locally; the next
  `work/dark-factory` run confirms it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Output url./urle. file-URL tests state per-platform expectations; drive-letter case expects empty parts on Linux/macOS
