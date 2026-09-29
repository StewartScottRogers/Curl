---
id: BL-512
title: Enforce --max-time and --connect-timeout for ftp and ftps
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-510, BL-511]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-512 — Enforce --max-time and --connect-timeout for ftp and ftps

## Goal

An FTP or FTPS transfer that outlasts `-m`, or whose control or data connection outlasts `--connect-timeout`, ends with exit 28 and the message curl 8.21.0 prints in that phase (including a data connection curl waits to accept in active mode, exit 12 where curl gives it).

## Context

- Conformance audit 2026-09-28, row 12 (Blocker). Mechanism: BL-498's ADR; connect phase: BL-510.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` and the handler; routing in `Curl.Console/RoutingFtpProtocolHandler.cs`. Active mode waits on `IConnectionListener` (ADR-0102).
- `Record-CurlExchange.ps1 -Ftp` serves FTP; add a stall option to it (for instance delay one named reply) if it has none, rather than writing another server.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp` (extended if needed): `-m 1` with the server stalling after the greeting, after `PASV`, and mid-`RETR`; `--connect-timeout 1` with a data connection that never connects; active mode (`-P -`) where the server never connects back; stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` tests on a fake `TimeProvider` pin each measured exit code and message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Ftp.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
