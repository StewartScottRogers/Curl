---
id: BL-5381
title: Skip leading spaces in a WinLDAP-dialect LDAP search filter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5381 — Skip leading spaces in a WinLDAP-dialect LDAP search filter

## Goal

In the WinLDAP dialect, an LDAP search filter with leading spaces (`%20(cn=a)`) skips them and sends the intended filter, as curl does, instead of a garbage equality filter.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-106 (Low, Confirmed: 2 shapes, twice; wrong search sent silently, exit 0). Area: LDAP search filter (WinLDAP dialect), Windows.

**Repro.** An LDAP server on 127.0.0.1:41024 that accepts a simple bind, answers any search with entry `cn=a,dc=x`, and logs the SearchRequest filter bytes (PowerShell or a C# file-based app).
- `curl -sS "ldap://127.0.0.1:41024/dc=x??base?%20(cn=a)" -u cn=r:pw`
- `curl -sS "ldap://127.0.0.1:41024/dc=x??base?%20%20(%26(cn=a)(sn=b))" -u cn=r:pw`

**Expected (REF).** Leading spaces skipped: equalityMatch `cn`=`a` (`...0402636e040161`), and AND(cn=a, sn=b).
**Actual (PORT).** equalityMatch with attribute ` (cn` and value `a)` (`04042028636e04026129`); the second sends attribute `  (&(cn`, value `a)(sn=b))`. Exit 0.
**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs` (WinLDAP dialect): the bare `attr=value` fallback runs before leading whitespace is trimmed.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove the filter ` (cn=a)` encodes as equalityMatch `cn`=`a` (`0402636e040161`) and `  (&(cn=a)(sn=b))` as AND of `cn=a` and `sn=b`.
- [ ] A test proves a filter without leading spaces, and a bare `attr=value` filter, encode as before.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: the server logs `0402636e040161` for the first command and an AND of `cn=a` and `sn=b` for the second.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the server logs `0402636e040161` for the first command and an AND of `cn=a` and `sn=b` for the second.
- The review's neighbouring finding STD-107 (an unescaped `(` in a nested item's value) touches the same encoder and is filed separately.

## Log

- 2026-11-17: Created.
