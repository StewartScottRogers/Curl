---
id: BL-683
title: Encode and decode NTLM negotiate, challenge and authenticate messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-682]
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-683 — Encode and decode NTLM negotiate, challenge and authenticate messages

## Goal

`Curl.Ntlm.UnitLibrary` writes the NTLM NEGOTIATE (Type 1) message curl 8.21.0 sends byte for byte, reads a server's CHALLENGE (Type 2) message including its target information AV pairs, and writes an AUTHENTICATE (Type 3) message from supplied responses, per MS-NLMP section 2.2.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design and seam: BL-525's ADR. Consumers: BL-526 (HTTP), BL-604 (proxy), BL-538 (SASL), BL-692 (SPNEGO), BL-595 (SMB).
- curl's own NTLM code is the reference for the bytes (`lib/vauth/ntlm.c` at tag `curl-8_21_0`: the flags it sets in Type 1, which fields it fills in Type 3, the workstation name, Unicode vs OEM); BL-526 measures the Type 1 bytes curl sends, and this task may use `Record-CurlExchange.ps1` itself for them (`--ntlm -u u:p` against a `401 NTLM`).
- MS-NLMP 2.2.1.1 to 2.2.1.3, 2.2.2 (AV_PAIR, VERSION, flags). Decoding is defensive: offsets and lengths are checked, a malformed Type 2 is a typed failure, never an exception from an out-of-range read.

## Acceptance criteria

- [ ] `Curl.Ntlm.UnitTests` pin the Type 1 bytes for the flag set curl sends (with the measurement or source line cited), decode MS-NLMP 4.2's example CHALLENGE messages and a Type 2 with target info, reject truncated and out-of-range messages with the typed failure, and pin a Type 3 layout for fixed inputs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
