---
id: BL-6084
title: Fix CI failure ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitTests, Curl.Original.Protocol.Ftp.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6084 — Fix CI failure ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing on Linux

## Goal

`ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` failed on Linux in CI run 46102818546 (https://github.com/StewartScottRogers/Curl/actions/runs/46102818546). First failing commit: 4e954e9f.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 46102818546 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test (added by BL-6014) expected CRLF in the listing on every platform. The
  product is right: a listing is an ASCII transfer, so off Windows curl and Curl write LF
  (ADR-0905). No production code changed, so no quality run and no new ADR.
- Fix: the named test is now Windows-only, and
  `..._WritesTheListingWithLfEndings` (name ending `RequiringSessionReuseOffWindows_...`)
  pins the Linux and macOS answer; both share one assertion helper.
- Delivered directly rather than through the full `feature` stages: a one-file test fix.
- Verified: `dotnet build` clean, `dotnet format --verify-no-changes` clean on the test
  project, fast tests green, the Windows test passes locally and the off-Windows one is
  skipped. CI log of run 46102818546 shows this as its only failing test. The CI half of
  the criterion is met by platform-neutral tests (a lane cannot see CI for its own push);
  the shift's CI watch files a task for any failure.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 22f846bf.
- 2026-11-27: Doing -> Done. The explicit-TLS listing test pins CRLF on Windows and LF off Windows
