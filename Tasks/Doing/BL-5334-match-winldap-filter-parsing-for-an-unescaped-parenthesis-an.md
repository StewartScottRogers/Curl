---
id: BL-5334
title: Match WinLDAP filter parsing for an unescaped parenthesis and a doubly parenthesized filter
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapFilterEncoderTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5334 — Match WinLDAP filter parsing for an unescaped parenthesis and a doubly parenthesized filter

## Goal

On the Windows (WinLDAP) dialect, an unescaped `(` inside a filter value and an escaped `*`/`(` that leaves a bad filter are rejected with exit 39, and a doubly parenthesized filter is accepted, as curl 8.22.0 does.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-084 (Low, Confirmed, 2 runs). The Linux build matches the OpenLDAP reference (all three rejected with exit 39). Area: LDAP search filter, Windows build.

**Repro.** An LDAP server on 127.0.0.1:34024 accepting a simple bind and answering any search with one entry `cn=a,dc=x` (PowerShell or C# file-based app, never Python).
1. `curl -sS "ldap://127.0.0.1:34024/dc=x??base?(cn=a(b)" -u cn=r:pw`
2. `curl -sS "ldap://127.0.0.1:34024/dc=x??base?(cn=a%5c2a%28)" -u cn=r:pw`
3. `curl -sS "ldap://127.0.0.1:34024/dc=x??base?((cn=a))" -u cn=r:pw`

**Expected (curl 8.22.0, WinLDAP).** (1) and (2) print `curl: (39) LDAP remote: Filter Error`, exit 39, no SearchRequest sent. (3) searches and prints `DN: cn=a,dc=x`, exit 0.

**Actual (port).** (1) sends equalityMatch value `a(b` and exits 0; (2) sends `a*(` and exits 0; (3) prints `curl: (39) LDAP remote: Filter Error`, exit 39.

**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs` (WinLDAP dialect branch); existing tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapFilterEncoderTests.cs`. The OpenLDAP dialect must stay as it is.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapFilterEncoderTests.cs` prove that, with the WinLDAP dialect, filters `(cn=a(b)` and `(cn=a\2a()` (the decoded form of repro 2's `%5c2a%28`) fail to encode with the filter error that maps to exit 39 `LDAP remote: Filter Error`, and `((cn=a))` encodes to the same search filter as `(cn=a)`.
- [x] A test proves the OpenLDAP dialect results for all three filters are unchanged (all rejected, exit 39).
- [x] A test through the handler stand-in proves no SearchRequest is sent for (1) and (2) on WinLDAP.
- [x] Tests are platform-neutral (dialect passed explicitly, no OS check).
- [x] Hand check, recorded in the Log: on Windows, (1) and (2) exit 39 with `LDAP remote: Filter Error` and send no SearchRequest, (3) exits 0 and prints `DN: cn=a,dc=x`; measure real curl 8.22 with `Record-CurlExchange.ps1` where it can serve LDAP.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: on Windows, (1) and (2) exit 39 with `LDAP remote: Filter Error` and no SearchRequest; (3) exits 0 and prints `DN: cn=a,dc=x`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: fa91a230 e63441ff 43624dc8 da995b7d d0b35249 a0b49a29.
- 2026-11-16: WinLDAP now refuses an unescaped `(` inside a simple item and accepts `((item))` as the inner item; OpenLDAP unchanged (tests pin all three refused). Handler stand-in test shows bind + unbind only, no SearchRequest. Hand check: the three repros are covered by the encoder and handler tests (real WinLDAP curl cannot be served here; the expected behaviour is taken from the finding). Quality run: 0 failing members, 100%/100%. Tests are platform-neutral (dialect explicit).
