---
id: BL-4028
title: Fix CI failure AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-4028 — Fix CI failure AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes on macOS

## Goal

`AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes` failed on macOS in CI run 40810061535 (https://github.com/StewartScottRogers/Curl/actions/runs/40810061535). First failing commit: 6407f459.

    Assertion failed. Expected exception of type IOException (or derived) but no exception was thrown.

Lanes test only on Windows, so reproduce with `gh run view 40810061535 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `AuthenticateAsClientAsync_ServerCertificateRefused_ServerHandshakeNeverCompletes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
