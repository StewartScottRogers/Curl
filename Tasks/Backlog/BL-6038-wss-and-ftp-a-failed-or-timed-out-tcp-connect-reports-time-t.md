---
id: BL-6038
title: wss and ftp: a failed or timed-out TCP connect reports %{time_total} 0; curl reports the elapsed time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6028]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerConnectFailureTimingTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6038 — wss and ftp: a failed or timed-out TCP connect reports %{time_total} 0; curl reports the elapsed time

## Goal

A `wss`, `ws` or `ftp` transfer whose TCP connect fails or times out reports `%{time_total}` as the elapsed time, as curl does.

## Context

Found while measuring BL-6028 (2026-11-26, Windows, curl 8.21.0 Schannel as the real curl).
`-s -m 3 -w "%{exitcode} %{time_total} %{num_connects}"`:

- `wss://10.255.255.1/` and `ftp://10.255.255.1/`: real `28 3.01 0`; Curl `28 0.000000 0`.
- `wss://127.0.0.1:1/` (refused) already reports a non-zero `%{time_total}`.
- `wss://nonexistent.invalid/`: real `6 0.008 0`; Curl `6 0.000000 0`.

The handler returns a failure with no report, and `EndPointReportingProtocolHandler.StampTimingsAndUploadSize`
leaves such a result unstamped (BL-5421 says why for the cases it measured; keep those right).
`%{num_connects}` stays 0 for these, as real curl's does.

## Acceptance criteria

- [ ] `wss` and `ftp` connects that fail (exit 6, 28) report a non-zero `%{time_total}`, with `%{num_connects}` 0, matching real curl measured with the same command lines.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
