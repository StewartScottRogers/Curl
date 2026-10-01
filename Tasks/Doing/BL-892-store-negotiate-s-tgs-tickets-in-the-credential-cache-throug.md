---
id: BL-892
title: Store Negotiate's TGS tickets in the credential cache through a disk-backed Kerberos file writer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-825]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-892 — Store Negotiate's TGS tickets in the credential cache through a disk-backed Kerberos file writer

## Goal

Negotiate's hand-built Kerberos route stores a service ticket it got by a TGS exchange back in the default `FILE:` or `DIR:` credential cache on disk, so the next curl run for the same host asks no KDC.

## Context

- Follow-up from BL-825 (ADR-0208): `Curl.Kerberos.UnitLibrary` now has `IKerberosFileWriter`, `CredentialCacheStore.Store` and `KerberosKdcClient.GetServiceTicketAsync(server, store, cacheName, ...)`.
- `Curl.Authentication.UnitLibrary/KerberosServiceTicketSource.cs` still reads a `CredentialCache` and calls the read-only overload; `Curl.Console/HandBuiltKerberosSources.cs` builds `CredentialCacheStore` with no writer.
- The disk-backed writer appends to an existing file only (never creates it) and lives in `Curl.Console` beside the existing `IKerberosFileReader` implementation, excluded from coverage only if it is a thin `System.IO` adapter as ADR-0083 allows.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` show `KerberosServiceTicketSource` getting a ticket through the storing overload with the default cache name, and a second `GetAsync` for the same host making no KDC exchange.
- [ ] `Curl.Console` passes a disk-backed `IKerberosFileWriter` to `CredentialCacheStore`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Authentication.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
