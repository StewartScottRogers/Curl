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
completed:
---
# BL-320 — Make ForPlatform_Windows_IsTheSystemAnsiCodePage pass on Linux and macOS CI

## Goal

`ForPlatform_Windows_IsTheSystemAnsiCodePage` passes on ubuntu-latest and macos-latest in the CI workflow, as it already does on windows-latest, and still proves what it set out to prove on each platform.

## Context

CI (`.github/workflows/ci.yml`) runs the fast tests on Windows, Linux and macOS. On `work/dark-factory` it has been red on Linux and macOS since at least 2026-09-27 05:59Z (run 36305641380), and this is one of four tests that fail there. `Curl.Authentication.UnitTests/CredentialEncodingTests.cs`: The test asks for the Windows ANSI code page on a machine that has none, so it fails outside Windows.

Curl publishes native binaries for all three platforms (BL-028) and matches the platform's own curl (ADR-0009), so the fix is to make the test express the right expectation per platform - not to skip it on non-Windows unless the behaviour genuinely exists only on Windows, in which case say so in the test name.

## Acceptance criteria

- [ ] The test passes on Windows locally, and its expectation for Linux and macOS is stated in the test (a platform-specific case or data row), matching what the platform's curl or .NET actually does there.
- [ ] If production code was wrong on Linux or macOS rather than the test, it is fixed, with coverage kept at 100%.
- [ ] The next CI run of `work/dark-factory` no longer lists this test as failing on ubuntu-latest or macos-latest.

## Notes

## Log

- 2026-09-27: Created.
