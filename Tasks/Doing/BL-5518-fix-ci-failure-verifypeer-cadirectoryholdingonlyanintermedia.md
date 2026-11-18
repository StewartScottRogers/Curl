---
id: BL-5518
title: Fix CI failure VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60 on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-18
completed:
---
# BL-5518 — Fix CI failure VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60 on Linux

## Goal

`VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60` failed on Linux in CI run 44411823897 (https://github.com/StewartScottRogers/Curl/actions/runs/44411823897). First failing commit: b6bb156b.

    Test method Curl.Networking.ServerCertificateVerificationCaDirectoryIntermediateTests.VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 44411823897 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_CaDirectoryHoldingOnlyAnIntermediateThatExcludesTheName_IsExit60` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: the test CA helper gave the intermediate notAfter = now+30d while the issuer's notAfter (truncated to seconds, made earlier) was slightly earlier, so CertificateRequest.Create threw on Linux. Fix: the child ends one day before its issuer. Test-only; CI on Linux/macOS met by the platform-neutral change.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 802308c8.
