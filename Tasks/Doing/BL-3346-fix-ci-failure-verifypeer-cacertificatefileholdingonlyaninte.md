---
id: BL-3346
title: Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3346 — Fix CI failure VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts on Linux

## Goal

`VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts` failed on Linux in CI run 40008612979 (https://github.com/StewartScottRogers/Curl/actions/runs/40008612979). First failing commit: a07ed80b.

    Test method Curl.Networking.ServerCertificateVerificationPartialChainTests.VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts threw exception:

Lanes test only on Windows, so reproduce with `gh run view 40008612979 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild_Accepts` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a164e65a.
