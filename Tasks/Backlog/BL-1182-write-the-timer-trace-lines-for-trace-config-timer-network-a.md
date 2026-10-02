---
id: BL-1182
title: Write the [TIMER] trace lines for --trace-config timer, network and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1182 — Write the [TIMER] trace lines for --trace-config timer, network and -vvvv

## Goal

Curl writes curl 8.21.0's `[TIMER]` lines under `--trace-config timer`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment). Measured 2026-10-02 (BL-1159 Notes): `[TIMER] [HAPPY_EYEBALLS] cleared` after `Trying` on a direct connect that succeeds; for `localhost` with both families, `[TIMER] [HAPPY_EYEBALLS] set for 200000ns` and `[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms` after the first `Trying`. Written from the happy eyeballs loop in `Curl.Networking`'s `TcpConnector`, beside `SetupFilterTraceEvents`.

## Acceptance criteria

- [ ] Tests pin the `[TIMER]` lines of a plain connect, a `localhost` connect where both families answer, and a refused connect, and that none appears without `timer`, `network` or `all`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
