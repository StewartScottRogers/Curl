---
id: BL-789
title: Read DIR and KCM Kerberos credential caches
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-688]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-789 — Read DIR and KCM Kerberos credential caches

## Goal

`Curl.Kerberos.UnitLibrary` reads the default credential cache when `KRB5CCNAME` (or `krb5.conf`'s `default_ccache_name`) names a `DIR:` collection (its `primary` file, then the `FILE:`-format caches it names) or a `KCM:` cache (the KCM protocol over the Unix socket `/var/run/.heim_org.h5l.kcm-socket` or the configured `kcm_socket`, as MIT's `cc_kcm.c` speaks it), so the hand-built Kerberos route finds a `kinit` ticket in every cache type ADR-0142 assigns to it.

## Context

- ADR-0142 (BL-525): off Windows, Negotiate and Kerberos use the system GSS-API library when it answers and the hand-built route only when it answers `Unsupported`; the hand-built route reads `FILE:` (BL-688), `DIR:` and `KCM:`. `KEYRING:` and macOS `API:` are left to the system library by that ADR.
- MIT Kerberos documentation "ccache types" (`DIR:` layout and `primary`) and `src/lib/krb5/ccache/cc_kcm.c` / `kcm.h` (opcodes `GET_DEFAULT_CACHE`, `GET_PRINCIPAL`, `GET_CRED_UUID_LIST`, `GET_CRED_BY_UUID`; message framing: 4-byte big-endian length, version 2.0, opcode).
- The library never opens a socket or a file itself (ADR-0120): directory listing, file reads and the KCM socket exchange go through injected seams; the Unix-socket adapter lives in `Curl.Networking.UnitLibrary` and is composed by the task that wires Kerberos into `Curl.Console` (BL-527), so tests need no disk and no daemon.

## Acceptance criteria

- [ ] A `DIR:` cache test: with `KRB5CCNAME=DIR:/d` and a fake directory whose `primary` names `tkt1`, the TGT in `/d/tkt1` (FILE format v4 bytes from BL-688's test data) is returned; `DIR::/d/tkt2` selects `tkt2` directly.
- [ ] A `KCM:` cache test: a scripted KCM exchange returning the default cache name, principal, one UUID and its credential yields that credential; a KCM error reply is a typed failure, not an exception.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, with `Curl.Kerberos.UnitLibrary` at 100% line and branch coverage.

## Notes

- Filed by BL-525 as the follow-up ADR-0142 names.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
