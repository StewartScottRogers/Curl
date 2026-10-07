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
completed: 2026-10-02
---
# BL-1176 — Fix CI failure AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion on macOS

## Goal

`AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` failed on macOS in CI run 37015514968 (https://github.com/StewartScottRogers/Curl/actions/runs/37015514968). First failing commit: 7224d307.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37015514968 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: only the TLS 1.3 rows failed. They ran against an `SslStream` server, and macOS's
  `SslStream` cannot serve TLS 1.3, so the handshake ended with a protocol-version alert
  (exit 35). `HandBuiltTlsConnectionClearTlsTests` already excludes its TLS 1.3 `SslStream`
  tests on macOS for the same reason.
- Fix (test only, no production code): the named test now runs all four legacy-minimum rows
  against a TLS 1.2 server, which passes on every platform. The TLS 1.3 rows moved to
  `AuthenticateAsClientAsync_WithALegacyMinimumAgainstATls13Server_NegotiatesTls13`, marked
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]`.
- Verified on Windows: Curl.Networking.UnitTests 2723 passed, all 33 fast-test assemblies
  green. Lanes cannot run CI. The shift's CI watch checks Linux and macOS after integration
  and files a new task if either fails.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Legacy-minimum TLS test passes on macOS: TLS 1.3 rows split into a test excluded on macOS
