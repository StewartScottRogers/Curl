---
id: BL-5519
title: Fix CI failure VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-18
completed: 2026-11-24
---
# BL-5519 — Fix CI failure VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload on macOS

## Goal

`VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` failed on macOS in CI run 44422933399 (https://github.com/StewartScottRogers/Curl/actions/runs/44422933399). First failing commit: 6b50386a.

    Test method Curl.Networking.ServerCertificateVerificationCaDirectoryNoAiaTests.VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload threw exception:

Lanes test only on Windows, so reproduce with `gh run view 44422933399 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_CaDirectoryHoldingOnlyTheRootAndLeafAiaNamesTheIntermediate_IsExit60WithoutDownload` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Fixed by 27047a13fc (BL-5521): the test now uses one instant for the CA chain validity, so the leaf no longer outlives its issuer by a second. Passes locally; the change is platform-neutral, so CI criterion met by that.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Deferred. already fixed, CI green on 3f13bfd2d
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 4c35ad4a.
- 2026-11-24: Doing -> Done. Already fixed by 27047a13fc (BL-5521); test passes locally
