---
id: BL-825
title: Store tickets got from the KDC back in the Kerberos credential cache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-825 — Store tickets got from the KDC back in the Kerberos credential cache

## Goal

A service ticket `KerberosKdcClient` gets by a TGS exchange is written back to the `FILE:` credential cache it came from, as MIT's `gss_init_sec_context` does, so the next request reuses it.

## Context

- Follow-up from BL-690.
- BL-690 reads the cache (BL-688, ADR-0158) but never writes it.
- MIT `src/lib/krb5/ccache/cc_file.c` (store), version 4 format; files through an injected writer seam, never `System.IO.File`.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` store a credential and read the cache back with `CredentialCacheReader` to the same credential, and a second `GetServiceTicketAsync` answers from the cache without a KDC exchange.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan and decision: ADR-0208. `CredentialCacheWriter.WriteCredential` mirrors `CredentialCacheReader` (a recorded MIT `kinit` cache rewrites byte for byte); `CredentialCacheStore.Store` appends it through the new `IKerberosFileWriter` (optional constructor parameter, so existing callers compile unchanged) to a `FILE:` or `DIR:` cache, as `cc_file.c` does; `KerberosKdcClient.GetServiceTicketAsync(server, store, cacheName, ...)` reads the cache and stores a TGS-got ticket, ignoring a failed store as MIT's `(void) krb5_cc_store_cred` does.
- Defaults taken: a missing start time is stored as the authentication time and a non-renewable ticket's renew-until as the epoch (MIT's `krb5_kdcrep2creds` layout); the file is never created (`KRB5_FCC_NOFILE`); `KCM:` is `UnsupportedType`; no writer is the new `KerberosFileError.NotWritable` (appended last so no numeric value moves). `KerberosFileException`'s message now says "read or written".
- `KerberosCredential` gained `StartTime`, `RenewUntil` and `Addresses`, filled from the KDC reply and from the cache, so a stored ticket keeps them.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0208 and its index line; no task in `Doing` names it.
- Verification 2026-09-29: `dotnet build Curl.slnx -warnaserror` clean; `Curl.Kerberos.UnitTests` 547/547; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10; the solution-wide fast run is green.
- Follow-ups filed: BL-892 (Negotiate and `Curl.Console` use the storing overload with a disk writer), BL-891 (`KCM_OP_STORE`).
- Rerun on lane 6 (2026-09-29): lane 1's work cherry-picked from `factory/BL-825-lane-1-20260929-023709` onto the current tree; conflicts with BL-816's `readUserName` parameter and `PathTokenInvalid` resolved by keeping both (`fileWriter` now follows `readUserName`); the ADR renumbered to ADR-0208 and the follow-ups refiled as BL-891 and BL-892, since ADR-0200, BL-879 and BL-880 are taken on master.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-825-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-825-lane-1-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A ticket got by TGS is appended to its FILE: or DIR: credential cache and the next request answers from the cache
