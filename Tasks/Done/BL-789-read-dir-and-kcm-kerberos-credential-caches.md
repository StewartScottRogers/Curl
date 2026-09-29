---
id: BL-789
title: Read DIR and KCM Kerberos credential caches
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-688]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-789 — Read DIR and KCM Kerberos credential caches

## Goal

`Curl.Kerberos.UnitLibrary` reads the default credential cache when `KRB5CCNAME` (or `krb5.conf`'s `default_ccache_name`) names a `DIR:` collection (its `primary` file, then the `FILE:`-format caches it names) or a `KCM:` cache (the KCM protocol over the Unix socket `/var/run/.heim_org.h5l.kcm-socket` or the configured `kcm_socket`, as MIT's `cc_kcm.c` speaks it), so the hand-built Kerberos route finds a `kinit` ticket in every cache type ADR-0142 assigns to it.

## Context

- ADR-0142 (BL-525): off Windows, Negotiate and Kerberos use the system GSS-API library when it answers and the hand-built route only when it answers `Unsupported`; the hand-built route reads `FILE:` (BL-688), `DIR:` and `KCM:`. `KEYRING:` and macOS `API:` are left to the system library by that ADR.
- MIT Kerberos documentation "ccache types" (`DIR:` layout and `primary`) and `src/lib/krb5/ccache/cc_kcm.c` / `kcm.h` (opcodes `GET_DEFAULT_CACHE`, `GET_PRINCIPAL`, `GET_CRED_UUID_LIST`, `GET_CRED_BY_UUID`; message framing: 4-byte big-endian length, version 2.0, opcode).
- The library never opens a socket or a file itself (ADR-0120): directory listing, file reads and the KCM socket exchange go through injected seams; the Unix-socket adapter lives in `Curl.Networking.UnitLibrary` and is composed by the task that wires Kerberos into `Curl.Console` (BL-527), so tests need no disk and no daemon.

## Acceptance criteria

- [x] A `DIR:` cache test: with `KRB5CCNAME=DIR:/d` and a fake directory whose `primary` names `tkt1`, the TGT in `/d/tkt1` (FILE format v4 bytes from BL-688's test data) is returned; `DIR::/d/tkt2` selects `tkt2` directly.
- [x] A `KCM:` cache test: a scripted KCM exchange returning the default cache name, principal, one UUID and its credential yields that credential; a KCM error reply is a typed failure, not an exception.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, with `Curl.Kerberos.UnitLibrary` at 100% line and branch coverage.

## Notes

- Filed by BL-525 as the follow-up ADR-0142 names.
- Lane 1 rerun (2026-09-29): cherry-picked lane 5's `feat(kerberos)` commit from `factory/BL-789-lane-5-20260929-023709`. On today's `factory/lane-1` it builds clean and every fast test passes (the earlier failure came from the other lanes' work, not this task's); `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary`: 100% line, 100% branch, worst CRAP 10.
- The decisions are recorded in ADR-0193, "Decided by Claude under Stewart's delegation". `Documentation/Planning/Decisions` was added to `touches` for it: lane 5 had to file a separate ADR task (its BL-877, never integrated) because BL-610 held that folder then; no task in Doing names it now, so the ADR is written here and no follow-up task is needed. `CredentialCacheStore`'s summary cites ADR-0193.
- Decided by Claude under Stewart's delegation (ADR-0193):
  - `DIR:/d` reads `/d/primary` as MIT `cc_dir.c`'s `read_primary_file` does: the first line must end in a newline and name a file starting `tkt`; a `/` in it is refused too. Anything else is `KerberosFileError.DirectoryPrimaryMalformed`. No `primary` means `tkt`. `DIR::/d/tkt2` reads that file directly.
  - `KRB5CCNAME` unset falls back to `krb5.conf`'s `default_ccache_name` (`%{uid}`, `%{euid}` expanded), then `FILE:/tmp/krb5cc_<uid>`.
  - `KCM:` connects to `kcm_socket`, else `/var/run/.heim_org.h5l.kcm-socket`; `-` turns it off. Failures are `KcmNotRunning`, `KcmFailed` (with `KcmStatus`) or `KcmReplyMalformed` (replies over 10 MiB included).
  - The KCM connector and configuration are optional constructor parameters, so `Curl.Console` compiles unchanged; BL-527 composes the Unix-socket connector.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 5 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-789-lane-5-20260929-023709; start with git cherry-pick --no-commit factory/BL-789-lane-5-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. DIR: and KCM: credential caches read by the hand-built Kerberos (ADR-0193); Curl.Kerberos.UnitLibrary at 100% line and branch coverage
