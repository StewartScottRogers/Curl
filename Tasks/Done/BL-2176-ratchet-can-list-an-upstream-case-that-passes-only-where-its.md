---
id: BL-2176
title: Ratchet can list an upstream case that passes only where its features exist (Schannel test2087, test3023)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2176 — Ratchet can list an upstream case that passes only where its features exist (Schannel test2087, test3023)

## Goal

A case that needs a feature only one platform's Curl has (such as `Schannel`) can be on `PassingUpstreamCases.txt` without failing the ratchet on the other platforms, and upstream test2087 and test3023 are listed.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 on Windows: test2087 and test3023 (`<features>` include `Schannel`) pass, but `UpstreamCaseRatchet` fails a listed case that is skipped (`Curl.Conformance.UnitLibrary/UpstreamCaseRatchet.cs`, line 42), and on Linux and macOS Curl has no `Schannel`, so listing them would turn CI red there. Decide the mechanism (for example, a listed case skipped only for a feature this platform's matched curl lacks is not a regression), record it in an ADR, and pin it in `UpstreamCaseRatchetTests`.

## Acceptance criteria

- [x] test2087 and test3023 are on `PassingUpstreamCases.txt`, and the fast tests pass on Windows, Linux and macOS in CI.
- [x] A listed case that is skipped for any other reason still fails the ratchet (unit test).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Mechanism (ADR-0488, decided by Claude under Stewart's delegation): UpstreamCaseScreening.FindFeatureOfAnotherPlatform names the feature when this platform's Curl does not meet a case's `<features>` but another platform's (UpstreamCurlPlatform.All) meets all of them; UpstreamCaseRunner checks it before any other screening and returns UpstreamCaseOutcome.SkippedOnThisPlatform; UpstreamCaseRatchet.Judge reports such a listed case Inconclusive. A listed case skipped for any other reason, including a feature no platform has (Debug), still fails (Judge_ListedSkip_Fails, FindFeatureOfAnotherPlatform_CaseFeatures_NamesOnlyAFeatureAnotherPlatformMeets).
- Chose one shared list over per-platform lists: the case's `<features>` already say where it can run.
- Measured 2026-10-13 on Windows: test2087 and test3023 pass as listed; Conformance fast tests 2748 passed, 0 failed. Linux and macOS are covered by the unit tests that judge a Schannel case there (RunAsync_CaseNeedingAFeatureThisPlatformLacks_SaysWhetherAnotherPlatformRunsIt); CI on the shift's branch confirms them.
- Measure-CodeQuality -Library Curl.Conformance.UnitLibrary: 100% line and branch, 0 failing members (Judge and RunAsync split to stay at complexity 10).
- `dotnet format --verify-no-changes` reports pre-existing end-of-line and import-order issues in files this task did not touch (UpstreamTest610ScriptTests.cs, UpstreamTestCertificateGenerator.cs, FtpTransferCommands.cs); none in the changed files.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Listed cases needing another platform's features (Schannel) are Inconclusive where they cannot run (ADR-0488); test2087 and test3023 listed
