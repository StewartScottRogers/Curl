---
id: BL-816
title: Honour krb5.conf default_ccache_name and default_keytab_name when finding the default cache and keytab
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-688, BL-689]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-816 — Honour krb5.conf default_ccache_name and default_keytab_name when finding the default cache and keytab

## Goal

`CredentialCacheStore` and `KeytabStore` in `Curl.Kerberos.UnitLibrary` find the default credential cache and keytab exactly as MIT Kerberos does when `krb5.conf` names them: `KRB5CCNAME`, then `[libdefaults] default_ccache_name`, then `FILE:/tmp/krb5cc_<uid>`; and `KRB5_KTNAME`, then `default_keytab_name`, then `FILE:/etc/krb5.keytab`, expanding MIT's `%{uid}`, `%{TEMP}` and the other parameter expansions the value may contain.

## Context

- BL-688 (ADR-0158) resolves the defaults from the environment and MIT's built-in names only, because `krb5.conf` is read by BL-689.
- MIT Kerberos documentation, `krb5.conf` "[libdefaults]" (`default_ccache_name`, `default_keytab_name`) and "Parameter expansion" list the expansions; Debian and Fedora ship `default_ccache_name = KEYRING:persistent:%{uid}`, which ADR-0142 leaves to the system GSS-API.
- Environment through `Func<string, string?>`, the uid through `Func<uint>`, as BL-688's stores take them.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` show `KRB5CCNAME` beating `default_ccache_name`, `default_ccache_name` beating the built-in name, and `%{uid}` expanded from the uid seam; the same three for the keytab.
- [x] Tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-688.

- The cache half already existed (BL-789 added `default_ccache_name` with `%{uid}`/`%{euid}`). BL-816 added `default_keytab_name` to `KeytabStore` and moved both stores onto a new `KerberosPathExpansion`, which expands every MIT Unix path token (MIT `expand_path.c`).
- Decisions are recorded in ADR-0207 (decided by Claude under Stewart's delegation): `%{uid}`/`%{euid}`/`%{USERID}`, `%{username}` (injected, default `Environment.UserName`), `%{TEMP}` (`TMPDIR` else `/tmp`), `%{LIBDIR}`/`%{BINDIR}`/`%{SBINDIR}` (MIT's `/usr/local` prefix), `%{null}`; case-sensitive; an unclosed or unknown token is `KerberosFileError.PathTokenInvalid`. `KeytabStore` now takes a required `Func<uint> readUserId`; nothing outside the tests constructed it.
- Lane 1's first run (branch factory/BL-816-lane-1-20260929-023709) failed integration on the fast tests; its feature commit was cherry-picked unchanged here and the whole fast suite passes, so the failure was the flaky `Curl.Networking.UnitTests` test lane 1 noted. Lane 1's plan to file the ADR as a separate task (it named it BL-878, an ID since taken by another task) was dropped: `Documentation/Planning/Decisions` sat in no Doing task's `touches` this time, so it was added to this task's `touches` and ADR-0207 was written here.
- `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary`: 100% line, 100% branch, 537 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-816-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-816-lane-1-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KeytabStore honours default_keytab_name and both stores expand MIT's Unix %{token} parameters (ADR-0207)
