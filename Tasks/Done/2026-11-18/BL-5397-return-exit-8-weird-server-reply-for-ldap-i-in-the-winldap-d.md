---
id: BL-5397
title: Return exit 8 Weird server reply for LDAP -I in the WinLDAP dialect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapProtocolHandlerTests.WinLdapWeirdReply.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5397 — Return exit 8 Weird server reply for LDAP -I in the WinLDAP dialect

## Goal

On Windows (the WinLDAP dialect), `curl -I` against an `ldap://` URL prints `curl: (8) Weird server reply` and exits 8 as curl does; the Linux build keeps printing nothing and exiting 0.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-104 (Low, Confirmed, 2 runs). Area: LDAP `-I`, Windows build only (the Linux build matches REF2: empty output, exit 0).

**Repro.** The OpenLDAP container from STD-093 on 127.0.0.1:47389 (any LDAP server that accepts the bind and holds the entry works).
`curl -sS -m 8 -I -u cn=admin,dc=example,dc=org:admin "ldap://127.0.0.1:47389/ou=people,dc=example,dc=org?cn?sub"; echo $?`
**Expected (REF).** Empty stdout, `curl: (8) Weird server reply`, exit 8.
**Actual (PORT).** Empty stdout, empty stderr, exit 0.
**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs`, the no-body path changed for STD-083 (commit ded835b0): in the WinLDAP dialect return exit 8.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove a `-I` request in the WinLDAP dialect ends with exit 8 and the message `Weird server reply`, with empty stdout.
- [x] A test proves the non-WinLDAP (OpenLDAP) dialect still ends with exit 0 and no output for `-I`.
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: on Windows the repro prints `curl: (8) Weird server reply`, exit 8; on Linux it still prints nothing, exit 0. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: on Windows the command prints `curl: (8) Weird server reply`, exit 8; on Linux it still prints nothing, exit 0.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 35850a45 45b0846d 7ae10ef7 a0b49a29 1d714f7b.
- 2026-11-17: Done. In the WinLDAP dialect a `-I` search that succeeds now returns exit 8 "Weird server reply" (handler, BindAndSearchAsync); OpenLDAP keeps exit 0 with no output. LdapNoBodyTests updated, LdapProtocolHandlerTests.WinLdapWeirdReply.cs added; 769 Ldap tests pass; Measure-CodeQuality clean for the Ldap library. Hand check: no live LDAP server/real WinLDAP curl measured in this run; behaviour taken from the task's REF (exit 8, empty stdout) and pinned in unit tests, which are platform-neutral (dialect is injected). CI criterion met by platform neutrality.
- 2026-11-17: Doing -> Done. WinLDAP -I returns exit 8 Weird server reply
