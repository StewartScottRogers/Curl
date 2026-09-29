---
id: BL-688
title: Read Kerberos credential caches and keytabs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-688 — Read Kerberos credential caches and keytabs

## Goal

`Curl.Kerberos.UnitLibrary` finds the default credential cache as MIT Kerberos does (`KRB5CCNAME`, then the default `FILE:/tmp/krb5cc_<uid>` form, and whatever else BL-525's ADR names) and reads its principal and credentials (the TGT and any service tickets) from the `FILE:` format version 4, and reads keytab files version 2, so Curl uses the ticket a user got with `kinit` exactly as curl's GSS-API build does.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). curl's GSS-API builds use the default credentials only; this is how the hand-built client gets them. Which cache types beyond `FILE:` (for example `DIR:`, `KCM:`, `KEYRING:`, the macOS `API:` cache) Curl reads, and how, is BL-525's ADR's call; file a follow-up task for each additional type it requires.
- Formats: MIT Kerberos documentation "The Kerberos Credential Cache File Format" and "Keytab File Format". Files are read through an injected file seam and the environment through the environment seam the codebase already uses, so tests need no disk.
- Test data: a cache and a keytab produced by MIT `kinit`/`ktutil` against a local KDC, committed as test bytes with the commands recorded in a comment.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` read the committed cache (principal, each credential's server, times, flags, session key type) and keytab (principal, KVNO, enctype, key), resolve the default cache name from `KRB5CCNAME` and from the fallback, and reject a truncated file and an unknown version with typed failures.
- [ ] Tests are platform-neutral (the uid comes from a seam, not the OS).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
