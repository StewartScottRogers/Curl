---
id: BL-2028
title: Find and fix the flaky Curl.Networking.UnitTests test that failed once in a full fast run and passed alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2028 — Find and fix the flaky Curl.Networking.UnitTests test that failed once in a full fast run and passed alone

## Goal

Curl.Networking.UnitTests passes in every full `dotnet test --filter "TestCategory!=Integration"` run, not only when run alone.

## Context

- Seen on 2026-10-10 by BL-1993 (lane 5): the full fast run reported `Failed: 1, Passed: 3151, Skipped: 29` for Curl.Networking.UnitTests; the project run alone right after passed. BL-1993 changed only the SMTP library, so the failure is not its. The failing test's name was not captured (the run's output was filtered to the summary lines).
- Start by running the full fast suite with `--logger trx` and reading the failing test from the TRX, or by looking for tests in the project that depend on timing, ports or shared state under load.

## Acceptance criteria

- [ ] The flaky test is named in Notes with its cause, and fixed so it no longer depends on timing or load.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
