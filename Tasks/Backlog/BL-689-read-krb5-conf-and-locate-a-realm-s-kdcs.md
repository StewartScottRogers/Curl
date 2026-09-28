---
id: BL-689
title: Read krb5.conf and locate a realm's KDCs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-689 — Read krb5.conf and locate a realm's KDCs

## Goal

`Curl.Kerberos.UnitLibrary` reads `krb5.conf` as MIT Kerberos does (`KRB5_CONFIG`, then the platform default path) for `default_realm`, `[realms]` `kdc` entries, `[domain_realm]` mappings, `dns_lookup_kdc`, `udp_preference_limit` and `default_tkt_enctypes`/`permitted_enctypes`, and locates a realm's KDCs from the file or, when allowed, from DNS SRV records `_kerberos._udp` and `_kerberos._tcp` through an injected SRV-lookup seam.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). MIT Kerberos documentation for `krb5.conf` (the relations, sections, `include`/`includedir`, comment rules) and for KDC location via DNS (RFC 4120 section 7.2.3).
- The SRV-lookup seam is an interface here; its implementation over the hand-built DNS client (BL-694) is composed where BL-525's ADR says (BL-527 wires it).
- File and environment reads go through injected seams; tests need no disk and no DNS.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` parse sample files covering every listed relation, `include`, comments and malformed lines as MIT does, map a host to its realm through `[domain_realm]` and the default, and return KDCs from the file first and from the fake SRV seam (ordered by priority and weight) when the file has none and `dns_lookup_kdc` allows it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
