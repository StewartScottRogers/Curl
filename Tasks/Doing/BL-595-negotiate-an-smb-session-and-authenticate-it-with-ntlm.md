---
id: BL-595
title: Negotiate an SMB session and authenticate it with NTLM
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-594, BL-684, BL-668, BL-532]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-595 — Negotiate an SMB session and authenticate it with NTLM

## Goal

On every platform, an `SmbProtocolHandler` connects through `IConnector`, sends the NetBIOS-framed negotiate request curl 8.21.0 sends, sets up a session with NTLM from `Curl.Ntlm.UnitLibrary` (BL-683, BL-684) as BL-594's ADR decides, and maps failures to curl's exit codes and messages (67 for a refused login).

## Context

- Conformance audit 2026-09-28, row 39. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SMB is offered on every platform (BL-594's ADR). Add the `Curl.Ntlm.UnitLibrary` reference (allowed once BL-668 lands) and amend `Curl.Protocol.Smb.UnitLibrary/CLAUDE.md` to name it.
- Measure off Windows with the reference OpenSSL-build curl through `Record-CurlExchange.ps1 -Script` (BL-532) or `-NoServer` against a local Samba server: negotiate and session setup succeeding and refused; record `request.bin`, stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smb.UnitTests` pin the negotiate and session-setup bytes (NTLM fields from fixed inputs) and the outcome for each case through a fake connection.
- [ ] Tests are platform-neutral and pass on Windows, Linux and macOS.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
