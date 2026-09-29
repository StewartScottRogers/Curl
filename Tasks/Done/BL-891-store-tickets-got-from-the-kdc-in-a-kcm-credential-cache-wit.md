---
id: BL-891
title: Store tickets got from the KDC in a KCM credential cache with KCM_OP_STORE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-825]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-891 — Store tickets got from the KDC in a KCM credential cache with KCM_OP_STORE

## Goal

`CredentialCacheStore.Store` stores a credential in a `KCM:` cache by sending MIT `cc_kcm.c`'s `KCM_OP_STORE` to the KCM daemon, instead of failing as `UnsupportedType`.

## Context

- Follow-up from BL-825 (ADR-0208), which stores in `FILE:` and `DIR:` caches only and leaves `KCM:` as `UnsupportedType`.
- BL-789 (ADR-0194) reads `KCM:` caches through `IKerberosKcmConnector`, `KerberosKcmClient` and `KcmCredentialCacheReader`; `KCM_OP_STORE` (opcode 6 in MIT `src/include/kcm.h`) takes the cache name and the credential marshalled as `CredentialCacheWriter.WriteCredential` writes it.
- An empty residual (`KCM:`) stores in the daemon's default cache, found with `KCM_OP_GET_DEFAULT_CACHE` as reading does.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` store a credential in `KCM:` and `KCM:name` through `FakeKcm` and check the request bytes are `KCM_OP_STORE` with the cache name and the marshalled credential; a non-zero status fails as `KcmFailed`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `KcmCredentialCacheWriter.Store` sends `KCM_OP_STORE` (opcode 6, `KerberosKcmOperation.Store`) with the cache name null-terminated and the credential as `CredentialCacheWriter.WriteCredential` marshals it, as MIT `cc_kcm.c`'s `kcm_store` does (`kcmreq_init` + `k5_marshal_cred` version 4); `KCM:` alone asks `KCM_OP_GET_DEFAULT_CACHE` first, reusing `KcmCredentialCacheReader.ReadDefaultCacheName`.
- A `KCM:` store needs no `IKerberosFileWriter`: it goes through the KCM connector, so `NotWritable` applies only to `FILE:` and `DIR:` caches. The KCM connection logic is shared with reading (`ConnectToKcm`: `kcm_socket`, `-` turns it off).
- `KerberosKcmClient` now zeroes each request buffer once sent, since a `STORE` request carries the session key.
- No ADR: behaviour follows MIT exactly, with no choice left open.
- Kerberos tests 587 passed; coverage 100% line and branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. CredentialCacheStore.Store stores a credential in a KCM: cache with KCM_OP_STORE, as MIT cc_kcm.c does
