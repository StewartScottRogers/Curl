---
id: BL-828
title: Offer the Kerberos encryption types krb5.conf permits by name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-828 — Offer the Kerberos encryption types krb5.conf permits by name

## Goal

Every KDC request offers the encryption types `permitted_enctypes` / `default_tkt_enctypes` / `default_tgs_enctypes` name, resolved as MIT's `krb5int_parse_enctype_list` does (`DEFAULT`, family names, `-name` removals), instead of ADR-0168's fixed list.

## Context

- Follow-up from BL-690.
- ADR-0168 offers a fixed list; `KerberosConfiguration` returns the names unresolved (ADR-0160).
- MIT `src/lib/krb5/krb/init_ctx.c` and `src/lib/crypto/krb/etypes.c` name tables.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` resolve MIT's documented examples of the three relations to type numbers, and `KerberosKdcClient` offers the resolved list in its AS-REQ and TGS-REQ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
