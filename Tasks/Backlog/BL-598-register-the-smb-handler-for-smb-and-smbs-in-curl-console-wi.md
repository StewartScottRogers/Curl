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

On platforms where BL-594's ADR offers SMB, `curl smb://...` and `curl smbs://...` run end to end through `Curl.Console` with curl 8.21.0's `-v` lines; on platforms where it does not, they are refused exactly as the reference build refuses them.

## Context

- Conformance audit 2026-09-28, row 39. Handler: BL-595 to BL-597.
- Register in `Curl.Console/CurlComposition.cs` behind the platform check the ADR states; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` accepts `smb`/`smbs` (port 445) only where offered; the `-V` protocol list follows ADR-0021.
- Measure `-v` for a download against Samba as in BL-595.

## Acceptance criteria

- [ ] Measured first as above; stderr copied into Notes with varying parts marked.
- [ ] `Curl.Console.UnitTests` pin an `smb://` download end to end on offering platforms and the refusal on the others, each under `OSCondition`.
- [ ] `curl -V` lists `smb` and `smbs` exactly where they are offered, with tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
