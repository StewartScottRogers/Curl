---
id: BL-5399
title: Accept an unescaped ( inside a nested WinLDAP filter item's value
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapWinLdapNestedUnescapedParenthesisFilterTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5399 — Accept an unescaped ( inside a nested WinLDAP filter item's value

## Goal

In the WinLDAP dialect, an unescaped `(` inside the value of a nested filter item (`(|(cn=a)(cn=b(c)))`) is accepted and searches for the value `b(c`, as WinLDAP does, instead of failing with exit 39.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-107 (Low, Confirmed, 3 shapes, twice). Area: LDAP search filter (WinLDAP dialect), Windows.

**Repro.** An LDAP server on 127.0.0.1:41024 that accepts a simple bind, answers any search with entry `cn=a,dc=x`, and logs the SearchRequest filter bytes.
`curl -sS "ldap://127.0.0.1:41024/dc=x??base?(|(cn=a)(cn=b(c)))" -u cn=r:pw`; also `(|(cn=b(c))(cn=a))` and `(%26(cn=a)(cn=b(c)))`.
**Expected (REF).** Searches with an OR/AND whose second item is equality `cn`=`b(c` (`...0402636e040462286329`), prints `DN: cn=a,dc=x`, exit 0. A top-level `(cn=a(b` is rejected by both.
**Actual (PORT).** `curl: (39) LDAP remote: Filter Error`, exit 39, no search sent.
**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs`: the STD-084 change (commit 497b2799) applies the top-level rule to nested items too.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove the three shapes encode an OR/AND whose item is equality `cn`=`b(c` (bytes `0402636e040462286329` for that item) and exit 0.
- [ ] A test proves a top-level `(cn=a(b` is still rejected (exit 39 Filter Error).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [ ] Hand check recorded in the Log: the three commands exit 0 and the server logs the `b(c` value. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- STD-106 (leading spaces) is filed separately and touches the same encoder; do not fold it in.
- Review acceptance check: the three commands exit 0 and the server logs the `b(c` value.

## Log

- 2026-11-17: Created.
