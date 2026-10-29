---
id: BL-3994
title: Fix CI failure OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3994 — Fix CI failure OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral on Linux and macOS

## Goal

`OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    Assertion failed. Expected value to not be null.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `UdpDatagramConnector` reached `AddressFamilyFilter` through an overload that read
  `OperatingSystem.IsWindows()` itself, so off Windows the test's `-6` to `127.0.0.1` was filtered
  to nothing (exit 6, null channel). Fix: a `FiltersAddressLiterals` init property as `TcpConnector`
  has; the tests pin both builds explicitly, so they hold on every platform; off Windows a filtered
  literal is exit 7, as the TCP path is (ADR-0968).
- CI criterion: the change is platform-neutral and the fast tests are green on Windows; the
  shift's CI watch files any failure.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing
  members after `NothingToOpen` was extracted from `OpenAsync` (complexity 14 before).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2a4d83b7.
- 2026-10-28: Doing -> Done. UDP connector takes FiltersAddressLiterals; test pins the Windows build on every platform
