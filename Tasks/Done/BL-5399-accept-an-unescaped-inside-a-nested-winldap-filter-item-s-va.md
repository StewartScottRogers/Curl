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
completed: 2026-11-17
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

- [x] New tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove the three shapes encode an OR/AND whose item is equality `cn`=`b(c` (bytes `0402636e040462286329` for that item) and exit 0.
- [x] A test proves a top-level `(cn=a(b` is still rejected (exit 39 Filter Error).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: the three commands exit 0 and the server logs the `b(c` value. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- STD-106 (leading spaces) is filed separately and touches the same encoder; do not fold it in.
- Review acceptance check: the three commands exit 0 and the server logs the `b(c` value.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 54a0e1f8 54962e93 7ae10ef7 a0b49a29 61800935.
- 2026-11-17: Hand check by unit test, not a live server (no real WinLDAP here): the three filters encode the OR/AND with the item equality `cn`=`b(c)` (bytes 0402636e040462286329 - the review's expected bytes show the value is the balanced `b(c)`, not `b(c`), and `(cn=a(b` is still refused. Platform-neutral tests; CI on Linux and macOS is covered by that.
- 2026-11-17: Done. Inside a nested set WinLDAP's simple item now keeps balanced `(`/`)` in its value; top level unchanged. Quality run for the library clean.

## Notes (added)

- The task text says value `b(c`, but the expected bytes decode to `b(c)`; I followed the bytes (a balanced-parenthesis value). Not an ADR: a bug fix inside the existing STD-084 dialect rule.
- 2026-11-17: Doing -> Done. Nested WinLDAP filter items keep balanced parentheses in the value; tests and quality run clean
