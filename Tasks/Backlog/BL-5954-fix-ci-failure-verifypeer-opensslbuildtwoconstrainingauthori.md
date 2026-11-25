---
id: BL-5954
title: Fix CI failure VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
---
# BL-5954 — Fix CI failure VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes on Linux

## Goal

`VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` failed on Linux in CI run 45604105442 (https://github.com/StewartScottRogers/Curl/actions/runs/45604105442). First failing commit: d3ffc4ee.

    Assertion failed. Expected strings to be equal.

Failing data rows: `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes (False,"excluded subtree violation (48)")`.

Lanes test only on Windows, so reproduce with `gh run view 45604105442 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-25: Created.
