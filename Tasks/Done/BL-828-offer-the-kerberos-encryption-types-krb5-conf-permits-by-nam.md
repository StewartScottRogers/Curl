---
id: BL-828
title: Offer the Kerberos encryption types krb5.conf permits by name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-828 — Offer the Kerberos encryption types krb5.conf permits by name

## Goal

Every KDC request offers the encryption types `permitted_enctypes` / `default_tkt_enctypes` / `default_tgs_enctypes` name, resolved as MIT's `krb5int_parse_enctype_list` does (`DEFAULT`, family names, `-name` removals), instead of ADR-0168's fixed list.

## Context

- Follow-up from BL-690.
- ADR-0168 offers a fixed list; `KerberosConfiguration` returns the names unresolved (ADR-0160).
- MIT `src/lib/krb5/krb/init_ctx.c` and `src/lib/crypto/krb/etypes.c` name tables.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` resolve MIT's documented examples of the three relations to type numbers, and `KerberosKdcClient` offers the resolved list in its AS-REQ and TGS-REQ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured MIT 1.22.1 (`kinit` from the extracted Ubuntu packages under WSL, AS-REQ read
  from `strace` against an unanswering KDC) for 17 relations; the table is in ADR-0209 and
  the cases are `KerberosEncryptionTypeListTests`. Findings: `DEFAULT` = 18 17 20 19 25 26
  (no des3 or rc4 in 1.22), `aes` = 18 17 20 19, `camellia` = 26 25, 24 needs
  `allow_weak_crypto`, single-DES names and numbers are unknown words, and the AS list is
  `default_tkt_enctypes` without intersecting `permitted_enctypes`.
- Design (ADR-0209, decided under Stewart's delegation): new `KerberosEncryptionTypeList`;
  `KerberosConfiguration` gains `DefaultTicketGrantingServiceEncryptionTypes` and
  `AllowWeakCrypto`; `KerberosKdcClient`'s static `RequestedEncryptionTypes` became
  instance `AsRequestEncryptionTypes` / `TgsRequestEncryptionTypes`, kept to the types the
  library has, and an empty list is `EncryptionTypeNotSupported` before sending.
- Default offer changes from 18 17 20 19 23 to 18 17 20 19: MIT 1.22 dropped rc4 from
  `DEFAULT`, and Camellia (25, 26) is not built yet - filed BL-893; des3 (16) is BL-894.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0209, its README row and a
  one-line amendment note in ADR-0168; no task in Doing names it.
- Pipeline run compactly in-session (plan, tests, implement, verify) rather than through
  the subagents; the gates are the same.
- Lane 6 rerun (2026-09-29): cherry-picked lane 1's feature commit onto the current branch,
  resolved the CLAUDE.md and ADR README conflicts beside BL-825's ADR-0208, renumbered the
  ADR from 0205 (now the TLS ClientHello ADR) to 0209, and refiled the follow-ups as
  BL-893 and BL-894. Build clean, all fast tests green (Kerberos 572), coverage 100/100.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-828-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-828-lane-1-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KDC requests offer default_tkt_enctypes / default_tgs_enctypes resolved as MIT 1.22's krb5int_parse_enctype_list does
