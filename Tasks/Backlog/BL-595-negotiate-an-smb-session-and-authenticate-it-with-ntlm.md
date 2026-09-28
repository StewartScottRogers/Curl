---
id: BL-595
title: Negotiate an SMB session and authenticate it with NTLM
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-594, BL-526, BL-532]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-595 — Negotiate an SMB session and authenticate it with NTLM

## Goal

On the platforms where BL-594's ADR offers SMB, an `SmbProtocolHandler` connects through `IConnector`, sends the NetBIOS-framed negotiate request curl 8.21.0 sends, sets up a session with NTLM through the seam BL-525 decided and BL-526 built, and maps failures to curl's exit codes and messages (67 for a refused login).

## Context

- Conformance audit 2026-09-28, row 39. If BL-594's ADR says SMB is offered nowhere, move this task to `Deferred` with that reason.
- Measure off Windows with the reference OpenSSL-build curl through `Record-CurlExchange.ps1 -Script` (BL-532) or `-NoServer` against a local Samba server: negotiate and session setup succeeding and refused; record `request.bin`, stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smb.UnitTests` pin the negotiate and session-setup bytes (NTLM fields from fixed inputs) and the outcome for each case through a fake connection.
- [ ] Tests are pinned only for platforms that offer SMB (`OSCondition`), and the not-offered platform's refusal lives in the registration task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
