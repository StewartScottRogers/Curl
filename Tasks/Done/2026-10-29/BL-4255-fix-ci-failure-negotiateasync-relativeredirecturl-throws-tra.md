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
completed: 2026-10-29
---
# BL-4255 — Fix CI failure NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect on Linux and macOS

## Goal

`NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` failed on Linux and macOS in CI run 40940836188 (https://github.com/StewartScottRogers/Curl/actions/runs/40940836188). First failing commit: 09a2c6ae.

    Assertion failed. Expected exception of exact type SignalRTransportException but caught InvalidOperationException.

Lanes test only on Windows, so reproduce with `gh run view 40940836188 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `NegotiateAsync_RelativeRedirectUrl_Throws_TransportProtocolsNegotiateRedirect` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: on Linux and macOS `Uri.TryCreate("/elsewhere", UriKind.Absolute, ...)` succeeds as `file:///elsewhere`, so `SignalRNegotiator` followed the redirect and failed with `InvalidOperationException`. Fix: a redirect URL is accepted only when it is absolute with scheme `http` or `https` (the negotiate redirect in `TransportProtocols.md` is an HTTP URL). New tests reject `file:///elsewhere` and `ftp://other.example/hub`, and the second-redirect test now redirects to `http://`, so every branch is covered on Windows. Small direct fix, so no `/feature` stages and no ADR.
- Passes locally on Windows (Curl.Extension.SignalR.UnitTests 89/89; full fast run green). The CI part is met by keeping the change platform-neutral: the scheme check does not depend on the OS. The shift's CI watch files a task for any failure.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.SignalR.UnitLibrary`: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 087aa2c2.
- 2026-10-29: Doing -> Done. SignalR negotiate rejects a rooted-path redirect URL on every platform: only absolute http(s) URLs are followed
