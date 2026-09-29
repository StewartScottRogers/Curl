---
id: BL-816
title: Honour krb5.conf default_ccache_name and default_keytab_name when finding the default cache and keytab
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-688, BL-689]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-816 — Honour krb5.conf default_ccache_name and default_keytab_name when finding the default cache and keytab

## Goal

`CredentialCacheStore` and `KeytabStore` in `Curl.Kerberos.UnitLibrary` find the default credential cache and keytab exactly as MIT Kerberos does when `krb5.conf` names them: `KRB5CCNAME`, then `[libdefaults] default_ccache_name`, then `FILE:/tmp/krb5cc_<uid>`; and `KRB5_KTNAME`, then `default_keytab_name`, then `FILE:/etc/krb5.keytab`, expanding MIT's `%{uid}`, `%{TEMP}` and the other parameter expansions the value may contain.

## Context

- BL-688 (ADR-0158) resolves the defaults from the environment and MIT's built-in names only, because `krb5.conf` is read by BL-689.
- MIT Kerberos documentation, `krb5.conf` "[libdefaults]" (`default_ccache_name`, `default_keytab_name`) and "Parameter expansion" list the expansions; Debian and Fedora ship `default_ccache_name = KEYRING:persistent:%{uid}`, which ADR-0142 leaves to the system GSS-API.
- Environment through `Func<string, string?>`, the uid through `Func<uint>`, as BL-688's stores take them.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` show `KRB5CCNAME` beating `default_ccache_name`, `default_ccache_name` beating the built-in name, and `%{uid}` expanded from the uid seam; the same three for the keytab.
- [ ] Tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-688.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-816-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-816-lane-1-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
