---
id: BL-688
title: Read Kerberos credential caches and keytabs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-688 — Read Kerberos credential caches and keytabs

## Goal

`Curl.Kerberos.UnitLibrary` finds the default credential cache as MIT Kerberos does (`KRB5CCNAME`, then the default `FILE:/tmp/krb5cc_<uid>` form, and whatever else BL-525's ADR names) and reads its principal and credentials (the TGT and any service tickets) from the `FILE:` format version 4, and reads keytab files version 2, so Curl uses the ticket a user got with `kinit` exactly as curl's GSS-API build does.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). curl's GSS-API builds use the default credentials only; this is how the hand-built client gets them. Which cache types beyond `FILE:` (for example `DIR:`, `KCM:`, `KEYRING:`, the macOS `API:` cache) Curl reads, and how, is BL-525's ADR's call; file a follow-up task for each additional type it requires.
- Formats: MIT Kerberos documentation "The Kerberos Credential Cache File Format" and "Keytab File Format". Files are read through an injected file seam and the environment through the environment seam the codebase already uses, so tests need no disk.
- Test data: a cache and a keytab produced by MIT `kinit`/`ktutil` against a local KDC, committed as test bytes with the commands recorded in a comment.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` read the committed cache (principal, each credential's server, times, flags, session key type) and keytab (principal, KVNO, enctype, key), resolve the default cache name from `KRB5CCNAME` and from the fallback, and reject a truncated file and an unknown version with typed failures.
- [x] Tests are platform-neutral (the uid comes from a seam, not the OS).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the plan is
  ADR-0142's (which already names the cache lookup) and the library was empty, so one
  session built readers, stores and tests against recorded bytes.
- Test bytes: a real cache and keytab from MIT Kerberos 1.22.1. WSL's Ubuntu had only
  the runtime libraries and no sudo, so `apt-get download` fetched `krb5-user`,
  `krb5-kdc`, `krb5-admin-server` and their libraries, `dpkg -x` unpacked them, and the
  KDC ran as the user on port 18888 (`[dbmodules] db_module_dir` pointed at the unpacked
  `db2.so`; `krb5kdc` needed `libevent-2.1-7t64` and `LD_PRELOAD` of
  `libverto-libevent.so.1`). The commands are in `RecordedKerberosFiles.cs`.
- MIT's `kinit` writes a `fast_avail` configuration entry (`X-CACHECONF:` realm) before
  the TGT; the reader keeps it and marks it (`CachedCredential.IsConfigurationEntry`).
- Decisions (ADR-0158, decided under Stewart's delegation): cache version 4 and keytab
  version 2 only, other versions `UnknownVersion`; name split at the first colon, no
  colon is `FILE`, no Windows drive-letter rule (ADR-0142 never takes this route on
  Windows); only `FILE:` caches and `FILE:`/`WRFILE:` keytabs, other types
  `UnsupportedType` (BL-789 adds `DIR:` and `KCM:`); defaults `KRB5CCNAME` then
  `FILE:/tmp/krb5cc_<uid>`, `KRB5_KTNAME` then `FILE:/etc/krb5.keytab`; typed failures
  through `KerberosFileException.Error`; file bytes and keys zeroed.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0158 and its index row; no
  task in Doing names it.
- Follow-up filed: BL-814 (honour `krb5.conf` `default_ccache_name` and
  `default_keytab_name`, after BL-689).
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Kerberos 72);
  `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary`: 100% line, 100% branch,
  47 members, 0 failing, worst CRAP 6.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Kerberos reads MIT FILE: credential caches (v4) and keytabs (v2) and finds the default cache from KRB5CCNAME or /tmp/krb5cc_<uid>
