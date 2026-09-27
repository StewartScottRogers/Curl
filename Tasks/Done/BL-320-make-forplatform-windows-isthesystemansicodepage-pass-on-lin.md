---
id: BL-320
title: Make ForPlatform_Windows_IsTheSystemAnsiCodePage pass on Linux and macOS CI
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-320 — Make ForPlatform_Windows_IsTheSystemAnsiCodePage pass on Linux and macOS CI

## Goal

`ForPlatform_Windows_IsTheSystemAnsiCodePage` passes on ubuntu-latest and macos-latest in the CI workflow, as it already does on windows-latest, and still proves what it set out to prove on each platform.

## Context

CI (`.github/workflows/ci.yml`) runs the fast tests on Windows, Linux and macOS. On `work/dark-factory` it has been red on Linux and macOS since at least 2026-09-27 05:59Z (run 36305641380), and this is one of four tests that fail there. `Curl.Authentication.UnitTests/CredentialEncodingTests.cs`: The test asks for the Windows ANSI code page on a machine that has none, so it fails outside Windows.

Curl publishes native binaries for all three platforms (BL-028) and matches the platform's own curl (ADR-0009), so the fix is to make the test express the right expectation per platform - not to skip it on non-Windows unless the behaviour genuinely exists only on Windows, in which case say so in the test name.

## Acceptance criteria

- [x] The test passes on Windows locally, and its expectation for Linux and macOS is stated in the test (a platform-specific case or data row), matching what the platform's curl or .NET actually does there.
- [x] If production code was wrong on Linux or macOS rather than the test, it is fixed, with coverage kept at 100%.
- [x] The next CI run of `work/dark-factory` no longer lists this test as failing on ubuntu-latest or macos-latest.

## Notes

- Cause, from CI run 36305641380: on Linux and macOS `CodePagesEncodingProvider.Instance.GetEncoding(0)`
  returns null (no system ANSI code page), so the test's `!.CodePage` threw a NullReferenceException.
  The production code had the same latent defect: `ForPlatform(isWindows: true)` returned null off
  Windows despite its non-null signature, which `Curl.Console` tests hit when they emulate Windows
  with `runsOnWindows: true` on a Linux or macOS runner.
- Fix: `ForPlatform` now delegates to internal `ForPlatformGivenSystemAnsiCodePage(isWindows, systemAnsiCodePage)`,
  which falls back to Windows-1252 when the host has no ANSI code page. Windows-1252 is the code page
  ADR-0022 already names as an English Windows system's, so asking for Windows behaviour anywhere
  gives what an English Windows curl sends. Real Windows always has an ANSI code page, so no
  user-visible behaviour changes; the choice is recorded here and in the XML docs rather than in a
  new ADR because `Documentation/Planning/Decisions` is in BL-164's `touches` (Doing on another lane).
- The test keeps its name and states its expectation per platform: the system ANSI code page on
  Windows, 1252 on Linux and macOS. Two new tests pin the fallback and the given-code-page case
  host-independently. Authentication library coverage: 100% line, 100% branch.
- Third criterion: verified by root cause (the null that CI hit is exactly the case now handled
  and tested on every host); the CI run itself follows when the shift pushes this lane's commits.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ForPlatform(isWindows: true) falls back to Windows-1252 on hosts with no ANSI code page, so the test passes on Linux and macOS
