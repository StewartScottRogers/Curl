---
id: BL-825
title: Store tickets got from the KDC back in the Kerberos credential cache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-825 — Store tickets got from the KDC back in the Kerberos credential cache

## Goal

A service ticket `KerberosKdcClient` gets by a TGS exchange is written back to the `FILE:` credential cache it came from, as MIT's `gss_init_sec_context` does, so the next request reuses it.

## Context

- Follow-up from BL-690.
- BL-690 reads the cache (BL-688, ADR-0158) but never writes it.
- MIT `src/lib/krb5/ccache/cc_file.c` (store), version 4 format; files through an injected writer seam, never `System.IO.File`.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` store a credential and read the cache back with `CredentialCacheReader` to the same credential, and a second `GetServiceTicketAsync` answers from the cache without a KDC exchange.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
