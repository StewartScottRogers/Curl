---
id: BL-598
title: Register the SMB handler for smb and smbs in Curl.Console with its -v lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-597]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-598 — Register the SMB handler for smb and smbs in Curl.Console with its -v lines

## Goal

On Windows, Linux and macOS, `curl smb://...` and `curl smbs://...` run end to end through `Curl.Console` with the `-v` lines of a curl 8.21.0 build that has SMB, as BL-594's ADR states.

## Context

- Conformance audit 2026-09-28, row 39. Handler: BL-595 to BL-597. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): offered on every platform, no platform check.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` accepts `smb`/`smbs` (port 445); the `-V` protocol list follows ADR-0021.
- Measure `-v` for a download against Samba as in BL-595.

## Acceptance criteria

- [ ] Measured first as above; stderr copied into Notes with varying parts marked.
- [ ] `Curl.Console.UnitTests` pin an `smb://` and an `smbs://` download end to end on every platform.
- [ ] `curl -V` lists `smb` and `smbs` on every platform, with tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
