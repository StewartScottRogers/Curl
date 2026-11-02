---
id: BL-4579
title: Fix CI failure Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4579 — Fix CI failure Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls on Linux and macOS and Windows

## Goal

`Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` failed on Linux and macOS and Windows in CI run 41541574110 (https://github.com/StewartScottRogers/Curl/actions/runs/41541574110). First failing commit: 021cdcee.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41541574110 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
