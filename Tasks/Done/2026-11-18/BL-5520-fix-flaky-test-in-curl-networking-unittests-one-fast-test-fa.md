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
completed: 2026-11-18
---
# BL-5520 — Fix flaky test in Curl.Networking.UnitTests: one fast test failed in one of four runs on Windows (2026-11-18)

## Goal

Curl.Networking.UnitTests' fast tests pass on every run, not most runs.

## Context

While BL-5485 ran on lane 2 (2026-11-18, commit f16fa94bff plus BL-5485's task change), `dotnet test --filter "TestCategory!=Integration"` ran Curl.Networking.UnitTests four times on Windows under load from other lanes. Two runs reported `Failed: 1, Passed: 4365, Skipped: 33` and two passed (`Passed: 4365, Skipped: 34`). The failing test's name was not captured, because `-v q` prints only the summary. It is probably a test that races concurrent work or depends on timing (note that the skipped count went from 34 to 33, so the failing test may be one that is sometimes skipped). Start by running the project with `--logger "trx"` in a loop and reading the failing test's name from the .trx. Then fix it to wait for the event it needs, as lane rule 9 says.

## Acceptance criteria

- [x] The flaky test is identified by name in Notes, along with the cause of the race.
- [x] The test waits for the event it needs instead of relying on timing, and 20 consecutive runs of Curl.Networking.UnitTests' fast tests pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Flaky test: ServerCertificateVerificationCaDirectoryNoAiaTests.VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload (4 of 20 runs failed). Cause: CreateCa read UtcNow twice, once for the root and once for the intermediate, and certificate times truncate to seconds, so when the clock crossed a second the intermediate's notAfter was later than the root's and CertificateRequest.Create threw ArgumentException. Fix: the intermediate's notAfter is the issuer's NotAfter minus a day.
- The varying skip count (33/34) was UdpChannelOpener_OpenFrom_BindsTheFirstFreePortOfTheRange going Inconclusive when parallel tests held the next port. It is an Inconclusive, not a failure; the two range tests now pick a bound port whose successor is free (BindUdpSocketWhosePortIsFollowedByAFreeOne), which narrows but does not remove it.
- After the fix 20 consecutive runs: 0 failed. Test-only change, so no Measure-CodeQuality run.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 14ea9dff 0f0ada24 3d836d5e.
- 2026-11-18: Doing -> Done. Flaky CA-directory AIA test fixed; 20 clean runs
