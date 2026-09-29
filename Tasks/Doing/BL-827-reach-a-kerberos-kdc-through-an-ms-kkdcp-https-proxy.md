---
id: BL-827
title: Reach a Kerberos KDC through an MS-KKDCP HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-827 — Reach a Kerberos KDC through an MS-KKDCP HTTPS proxy

## Goal

`KerberosKdcSender` reaches an `https://` KDC entry through an MS-KKDCP proxy (`KDC-PROXY-MESSAGE` over HTTPS POST) instead of skipping it.

## Context

- Follow-up from BL-690.
- ADR-0168 skips `https://` KDCs; `KerberosKdcLocator` already parses them (ADR-0160).
- MS-KKDCP; MIT `src/lib/krb5/os/sendto_kdc.c` (HTTPS transport). The HTTPS exchange needs its own injected seam, not a socket in this library.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` send an AS-REQ to an `https://` KDC through a fake proxy seam and read its reply, with the `KDC-PROXY-MESSAGE` bytes pinned against MIT's encoding.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
