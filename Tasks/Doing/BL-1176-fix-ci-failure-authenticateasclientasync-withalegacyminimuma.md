---
id: BL-1176
title: Fix CI failure AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-02
completed:
---
# BL-1176 — Fix CI failure AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion on macOS

## Goal

`AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` failed on macOS in CI run 37015514968 (https://github.com/StewartScottRogers/Curl/actions/runs/37015514968). First failing commit: 7224d307.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37015514968 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
