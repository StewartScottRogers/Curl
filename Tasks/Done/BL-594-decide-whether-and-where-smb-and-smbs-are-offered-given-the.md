---
id: BL-594
title: Decide how smb and smbs are built and offered on every platform
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-594 — Decide how smb and smbs are built and offered on every platform

## Goal

An ADR decides how Curl offers `smb://` and `smbs://` on Windows, Linux and macOS: which SMB dialect and messages curl speaks (measure it; curl's SMB support has historically been SMBv1 only), how NTLM from `Curl.Ntlm.UnitLibrary` authenticates the session, whose output text each platform matches, and how tests stay off the network.

## Context

- Conformance audit 2026-09-28, row 39 (Minor, L). `Curl.Protocol.Smb.UnitLibrary/CLAUDE.md`: `IConnection`, no `Socket`.
- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; output text still matches the platform's curl where both do the same thing. curl builds with SMB exist (the usual Linux OpenSSL builds list `smb smbs` under `Protocols:`), so SMB is offered everywhere, including Windows where the Schannel reference build (curl 8.21.0) has none; on Windows the `-v` and error text therefore come from a build that has SMB. The ADR decides HOW, never WHETHER.
- ADR-0021 (`-V` lists only what Curl implements) then lists `smb` and `smbs` on every platform once BL-598 lands. Measure `curl -V` and an `smb://` download against a local Samba server with a Linux or macOS OpenSSL build (`Record-CurlExchange.ps1 -NoServer` or `-Script`, BL-532); also record the Windows reference build's refusal text for the record only.
- NTLM: `Curl.Protocol.Smb.UnitLibrary` references `Curl.Ntlm.UnitLibrary` (allowed by BL-667's ADR and BL-668's test), built by BL-682 to BL-684. curl's SMB implementation (`lib/smb.c` at tag `curl-8_21_0`) is the reference for the messages.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured facts, stating that `smb`/`smbs` are offered on every platform and which build's text each platform matches.
- [x] It names the dialect, the messages needed, the NTLM route, and how tests stay off the network; its Consequences list BL-595 to BL-598.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- ADR-0200 decides it: SMBv1 `NT LM 0.12` as curl, NTLMv1 LM/NT responses via `NtlmResponseComputation.ComputeV1`, OpenSSL-build text on every platform, `IConnection` fixtures.
- Measured 2026-09-29: Windows curl 8.21.0 Schannel `curl: (1) Protocol "smb" is disabled`, exit 1. Linux (Ubuntu curl 8.18.0 in WSL) NEGOTIATE bytes recorded with `nc -l` because `Record-CurlExchange.ps1` runs only the Windows curl, which has no SMB; no Samba server was available, so server-side fixtures stay hand-assembled (noted in the ADR).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0200 decides smb/smbs: SMBv1 NT LM 0.12 with NTLMv1 responses on every platform, OpenSSL-build text, IConnection fixtures
