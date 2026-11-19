---
id: BL-5526
title: Fix flaky CI test VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5526 — Fix flaky CI test VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload that failed once on Windows

## Goal

`VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` failed on Windows in CI run 44432815224 (https://github.com/StewartScottRogers/Curl/actions/runs/44432815224). First failing commit: ee5dc7b4.

    Test method Curl.Networking.ServerCertificateVerificationCaDirectoryNoAiaTests.VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload threw exception:

Lanes test only on Windows, so reproduce with `gh run view 44432815224 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` fail intermittently is named under Notes and removed.
- [x] `VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: the test read DateTimeOffset.UtcNow separately for the intermediate CA and its issuer, so when the second changed between reads the intermediate notAfter outran its root (ArgumentException from CertificateRequest.Create). Same cause as BL-5521; commit 27047a13fc already uses one instant (s_certificateEpoch). The failing CI run predates it. Test passes locally; the fix is platform-neutral, so the Linux and macOS criterion is met by that.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 91f2482b 8581176e.
- 2026-11-18: Doing -> Done. Fixed by 27047a13fc (BL-5521): one instant for the CA chain's validity
