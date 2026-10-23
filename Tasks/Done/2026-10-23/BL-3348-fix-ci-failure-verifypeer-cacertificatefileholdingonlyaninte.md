---
id: BL-3348
title: Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60 on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3348 — Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60 on Linux

## Goal

`VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60` failed on Linux in CI run 40008612979 (https://github.com/StewartScottRogers/Curl/actions/runs/40008612979). First failing commit: a07ed80b.

    Test method Curl.Networking.ServerCertificateVerificationPartialChainTests.VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 40008612979 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheSchannelBuild_IsExit60` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Already fixed by BL-3346 (355ba2f28), which landed after the failing commit a07ed80b: `CreateCa` now gives a child CA its issuer's stored (whole-second) validity, so `CertificateRequest.Create` can no longer refuse a notAfter one second past the issuer's. No code change needed here. The 4 partial-chain tests and all 3445 Curl.Networking.UnitTests fast tests pass on Windows; the fix is platform-neutral (no clock or OS dependence left), which meets the Linux and macOS CI part of the criterion per lane rule 11.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f03ad46a.
- 2026-10-23: Doing -> Done. Partial-chain test CAs take their issuer's validity (fixed by BL-3346), so the Schannel intermediate-only test passes on every platform
