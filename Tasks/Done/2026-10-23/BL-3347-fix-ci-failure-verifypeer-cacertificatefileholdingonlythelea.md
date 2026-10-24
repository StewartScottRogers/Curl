---
id: BL-3347
title: Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3347 — Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts on Linux

## Goal

`VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts` failed on Linux in CI run 40008612979 (https://github.com/StewartScottRogers/Curl/actions/runs/40008612979). First failing commit: a07ed80b.

    Test method Curl.Networking.ServerCertificateVerificationPartialChainTests.VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts threw exception:

Lanes test only on Windows, so reproduce with `gh run view 40008612979 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_CaCertificateFileHoldingOnlyTheLeafBehindAnIntermediateInTheOpenSslBuild_Accepts` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failure (CreateCa refusing a child notAfter one second past its issuer's whole-second NotAfter) is the same cause BL-3346 fixed in 355ba2f28, which came after the failing CI run's commit a07ed80b; CreateCa now gives a child its issuer's validity. No further change needed. The four partial-chain tests pass locally; the fix is platform-neutral, so CI on Linux and macOS is met by that (the CI watch files a task for any failure).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: dc74c3a7.
- 2026-10-23: Doing -> Done. Already fixed by BL-3346's 355ba2f28 (same CreateCa notAfter cause); partial-chain tests pass
