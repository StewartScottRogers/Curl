---
id: BL-5520
title: Fix flaky test in Curl.Networking.UnitTests: one fast test failed in one of four runs on Windows (2026-11-18)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-5520 — Fix flaky test in Curl.Networking.UnitTests: one fast test failed in one of four runs on Windows (2026-11-18)

## Goal

Curl.Networking.UnitTests' fast tests pass on every run, not most runs.

## Context

While BL-5485 ran on lane 2 (2026-11-18, commit f16fa94bff plus BL-5485's task change), `dotnet test --filter "TestCategory!=Integration"` ran Curl.Networking.UnitTests four times on Windows under load from other lanes. Two runs reported `Failed: 1, Passed: 4365, Skipped: 33` and two passed (`Passed: 4365, Skipped: 34`). The failing test's name was not captured, because `-v q` prints only the summary. It is probably a test that races concurrent work or depends on timing (note that the skipped count went from 34 to 33, so the failing test may be one that is sometimes skipped). Start by running the project with `--logger "trx"` in a loop and reading the failing test's name from the .trx. Then fix it to wait for the event it needs, as lane rule 9 says.

## Acceptance criteria

- [ ] The flaky test is identified by name in Notes, along with the cause of the race.
- [ ] The test waits for the event it needs instead of relying on timing, and 20 consecutive runs of Curl.Networking.UnitTests' fast tests pass.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
