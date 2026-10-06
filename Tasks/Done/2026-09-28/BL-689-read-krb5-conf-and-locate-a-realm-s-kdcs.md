---
id: BL-689
title: Read krb5.conf and locate a realm's KDCs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-689 — Read krb5.conf and locate a realm's KDCs

## Goal

`Curl.Kerberos.UnitLibrary` reads `krb5.conf` as MIT Kerberos does (`KRB5_CONFIG`, then the platform default path) for `default_realm`, `[realms]` `kdc` entries, `[domain_realm]` mappings, `dns_lookup_kdc`, `udp_preference_limit` and `default_tkt_enctypes`/`permitted_enctypes`, and locates a realm's KDCs from the file or, when allowed, from DNS SRV records `_kerberos._udp` and `_kerberos._tcp` through an injected SRV-lookup seam.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). MIT Kerberos documentation for `krb5.conf` (the relations, sections, `include`/`includedir`, comment rules) and for KDC location via DNS (RFC 4120 section 7.2.3).
- The SRV-lookup seam is an interface here; its implementation over the hand-built DNS client (BL-694) is composed where BL-525's ADR says (BL-527 wires it).
- File and environment reads go through injected seams; tests need no disk and no DNS.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` parse sample files covering every listed relation, `include`, comments and malformed lines as MIT does, map a host to its realm through `[domain_realm]` and the default, and return KDCs from the file first and from the fake SRV seam (ordered by priority and weight) when the file has none and `dns_lookup_kdc` allows it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Touches widened to `Documentation/Planning/Decisions` for ADR-0160 and its index row; no task in Doing names it (BL-658: Http, BL-815: Tls).
- Plan, tests and implementation done in this session against the task text (feature pipeline, one library): `KerberosConfigurationReader` parses as `prof_parse.c`, `KerberosConfiguration` gives the relations with MIT defaults and `RealmOfHost`, `KerberosConfigurationStore` finds `KRB5_CONFIG` / `/etc/krb5.conf`, `KerberosKdcLocator` reads `kdc` entries then `_kerberos._udp`/`_tcp` SRV through `IKerberosSrvLookup`. Decisions in ADR-0160.
- `IKerberosFileReader` gained `ListFileNames` for `includedir`; its only implementer is the test fake.
- Measured without a local MIT build this run: parse rules follow MIT's `prof_parse.c` and `locate_kdc.c` behaviour as documented; SRV ordering is deterministic (priority asc, weight desc), recorded in ADR-0160.
- Gates: `dotnet build Curl.slnx -warnaserror` clean, fast tests green (Curl.Kerberos.UnitTests 191 passed), `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. krb5.conf is read as MIT does and a realm's KDCs are located from the file or SRV records
