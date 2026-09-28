---
id: BL-594
title: Decide whether and where smb and smbs are offered, given the Windows reference build lacks them
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-594 — Decide whether and where smb and smbs are offered, given the Windows reference build lacks them

## Goal

An ADR decides, per platform, whether Curl offers `smb://` and `smbs://`: refusing them as the Windows reference build does (curl 8.21.0 Schannel has no SMB), and offering them off Windows only if the usual OpenSSL builds have them, and, where offered, which SMB dialect curl speaks (measure it; curl's SMB support has historically been SMBv1 only) and what the handler must build.

## Context

- Conformance audit 2026-09-28, row 39 (Minor, L; "ADR first on platform split"). `Curl.Protocol.Smb.UnitLibrary/CLAUDE.md`: Abstractions only, `IConnection`.
- ADR-0009 (match the platform's usual build) and ADR-0021 (`-V` lists only what Curl implements) apply. Measure the Windows refusal (`curl smb://127.0.0.1/share/x`: stderr and exit code, likely exit 1 `Protocol "smb" not supported`), and `curl -V` on a Linux or macOS OpenSSL build.
- If SMB is offered anywhere, it needs NTLM (BL-525, BL-526); the implementation tasks BL-595 to BL-598 follow this ADR and are Deferred by their runner if the ADR says SMB is not offered.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured facts, stating per platform whether `smb`/`smbs` are offered and what Curl prints when they are not.
- [ ] If offered anywhere, it names the dialect, the messages needed, and how tests stay off the network; if offered nowhere, its Consequences say BL-595 to BL-598 are to be Deferred.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
