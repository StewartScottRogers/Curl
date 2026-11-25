---
id: BL-5521
title: Fix flaky CI test: one Curl.Networking.UnitTests case fails in the full fast run and passes alone
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5521 — Fix flaky CI test: one Curl.Networking.UnitTests case fails in the full fast run and passes alone

## Goal

Every Curl.Networking.UnitTests case passes in the full solution fast run on every run, not only when its project runs alone.

## Context

On 2026-11-18 lane 3 (BL-4496, which changed no Networking code) ran `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` at commit c00ea24d19 plus a docs/test-attribute change in Curl.Conformance.Extended.UnitTests: Curl.Networking.UnitTests reported `Failed: 1, Passed: 4364, Skipped: 34`. Running the project alone right after gave `Failed: 0, Passed: 4365`. The failing test's name was not captured. Likely a timing or shared-state race under load (method-level parallel, many test projects at once); a test that waits on real time or on scheduling order rather than the event it needs.

## Acceptance criteria

- [x] The flaky test is named under Notes (found by running the full fast run, or Curl.Networking.UnitTests repeatedly under load, with the failing name captured).
- [x] The test waits for the event it needs rather than on scheduling or real time, and passes in the full fast run.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-18 (lane 3): Filed from BL-4496's full fast run; see Context.
- 2026-11-18 (lane 3): Flaky test found by four concurrent runs of the project: `ServerCertificateVerificationCaDirectoryNoAiaTests.VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` (ArgumentException: intermediate notAfter one second later than the root's, because `CreateCa` read `DateTimeOffset.UtcNow` per certificate and the second rolled over). Fixed by one shared `s_certificateEpoch` for the whole chain. Build clean; Networking fast run 4366 passed, 0 failed. Test-only change, platform-neutral.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 5e00b497 c792f2bd 44447f62.
- 2026-11-18: Doing -> Done. CaDirectoryNoAia test uses one clock instant for its cert chain; no second-boundary race
