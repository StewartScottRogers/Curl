---
id: BL-6097
title: ftp and wss: a failed connect reports %{time_pretransfer} and %{time_starttransfer} 0; curl reports the elapsed time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6094]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerConnectFailureTimingTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6097 — ftp and wss: a failed connect reports %{time_pretransfer} and %{time_starttransfer} 0; curl reports the elapsed time

## Goal

A non-HTTP transfer of curl's own whose connect fails reports `%{time_pretransfer}` and `%{time_starttransfer}` as curl does.

## Context

Found while doing BL-6041 (2026-11-28, Windows, real curl 8.22.0 Schannel),
`-w "%{exitcode}|%{time_total}|%{time_pretransfer}|%{time_starttransfer}"`: for `ftp`, `ws`
and `wss` connects ending in exit 5, 6, 7 or 28, real curl prints both equal to the total
(`28|3.003770|3.003767|3.003767`, `7|0.000770|0.000767|0.000767`); Curl prints
`0.000000` for both. HTTP already matches. See `TimedReport` in
`Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` and ADR-1476. The BL-5493
tests pin no starttransfer for a transfer that connected and then failed: measure that
case before changing it.

## Acceptance criteria

- [ ] `ftp` and `wss` connects that fail with exit 6, 7 and 28 report `%{time_pretransfer}` and `%{time_starttransfer}` as measured from real curl with the same command lines.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins the behaviour (ADR-0482).
- [ ] `Measure-CodeQuality.ps1` reports no member this task changes or adds outside a gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
