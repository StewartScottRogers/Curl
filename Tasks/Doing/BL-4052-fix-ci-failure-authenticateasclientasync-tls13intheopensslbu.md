---
id: BL-4052
title: Fix CI failure AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4052 — Fix CI failure AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder on Windows

## Goal

`AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` failed on Windows in CI run 40832703845 (https://github.com/StewartScottRogers/Curl/actions/runs/40832703845). First failing commit: 0b744431.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40832703845 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f4b18e99.
