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
completed: 2026-09-30
---
# BL-892 — Store Negotiate's TGS tickets in the credential cache through a disk-backed Kerberos file writer

## Goal

Negotiate's hand-built Kerberos route stores a service ticket it got by a TGS exchange back in the default `FILE:` or `DIR:` credential cache on disk, so the next curl run for the same host asks no KDC.

## Context

- Follow-up from BL-825 (ADR-0208): `Curl.Kerberos.UnitLibrary` now has `IKerberosFileWriter`, `CredentialCacheStore.Store` and `KerberosKdcClient.GetServiceTicketAsync(server, store, cacheName, ...)`.
- `Curl.Authentication.UnitLibrary/KerberosServiceTicketSource.cs` still reads a `CredentialCache` and calls the read-only overload; `Curl.Console/HandBuiltKerberosSources.cs` builds `CredentialCacheStore` with no writer.
- The disk-backed writer appends to an existing file only (never creates it) and lives in `Curl.Console` beside the existing `IKerberosFileReader` implementation, excluded from coverage only if it is a thin `System.IO` adapter as ADR-0083 allows.

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests` show `KerberosServiceTicketSource` getting a ticket through the storing overload with the default cache name, and a second `GetAsync` for the same host making no KDC exchange.
- [x] `Curl.Console` passes a disk-backed `IKerberosFileWriter` to `CredentialCacheStore`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Authentication.UnitLibrary` and `Curl.Console`.

## Notes

- `KerberosServiceTicketSource` now takes `Func<CredentialCacheStore>` in place of `Func<CredentialCache>`; `GetAsync` resolves `DefaultCacheName()` once and calls the storing overload `GetServiceTicketAsync(server, store, cacheName, ...)`. When `krb5.conf` gives no realm for the host, the cache is read once more just for its default principal's realm; with a `[domain_realm]` match only the KDC client reads it (`GetAsync_DomainRealmConfigured_ReadsTheCacheOnlyInTheKdcClient`). The second criterion's test is `GetAsync_TicketGrantingTicketCached_StoresTheTgsTicketInTheDefaultCacheSoTheSecondGetAsksNoKdc`.
- `Curl.Console/KerberosDiskFileWriter` opens the existing file with `FileMode.Open` and seeks to its end, so it never creates one. Any `IOException` or `UnauthorizedAccessException` (missing file, a directory, no write permission) returns `false`, so `Store` fails and the KDC client uses the ticket unstored, as MIT ignores `krb5_cc_store_cred`'s failure. It is tested against a temporary directory like `KerberosDiskFileReader`, not excluded from coverage.
- Decided under Stewart's delegation, within ADR-0208: no file lock is taken (MIT's `cc_file.c` takes `fcntl` locks); the BCL has no portable advisory lock and a concurrent writer to the same cache is rare.
- `HandBuiltKerberosSources.ReadCredentialCache` became `CreateCredentialCacheStore`, built with the disk writer; the store still gets no `krb5.conf` or KCM connector, as before.
- 2026-09-30, lane 3: lane 2's work (branch `factory/BL-892-lane-2-20260930-140644`) was cherry-picked unchanged onto the current branch; it applied cleanly, `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library <each>` reports 100% line and branch for both libraries (0 failing members, worst CRAP 10). The earlier failure was the push, not the code.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Backlog. Lane 2 could not integrate: push kept being refused. The work is on branch factory/BL-892-lane-2-20260930-140644; start with git cherry-pick --no-commit factory/BL-892-lane-2-20260930-140644 and fix it.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Negotiate's hand-built Kerberos route stores a TGS-got service ticket in the default FILE:/DIR: cache on disk, so the next run asks no KDC
