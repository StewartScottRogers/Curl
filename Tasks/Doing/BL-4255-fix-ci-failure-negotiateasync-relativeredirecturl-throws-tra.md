---
id: BL-4255
title: Fix CI failure NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SignalR.UnitTests, Curl.Extension.SignalR.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4255 — Fix CI failure NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect on Linux and macOS

## Goal

`NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` failed on Linux and macOS in CI run 40940836188 (https://github.com/StewartScottRogers/Curl/actions/runs/40940836188). First failing commit: 09a2c6ae.

    Assertion failed. Expected exception of exact type SignalRTransportException but caught InvalidOperationException.

Lanes test only on Windows, so reproduce with `gh run view 40940836188 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 087aa2c2.
