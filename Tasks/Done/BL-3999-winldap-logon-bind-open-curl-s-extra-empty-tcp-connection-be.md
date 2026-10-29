---
id: BL-3999
title: WinLDAP logon bind: open curl's extra empty TCP connection before the WinLDAP connection (CGR-234 remainder)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitTests/Cgr234ConnectionTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3999 — WinLDAP logon bind: open curl's extra empty TCP connection before the WinLDAP connection (CGR-234 remainder)

## Goal

Without `-u`, Curl's Windows (WinLDAP) LDAP transfer opens curl's own empty TCP connection before the connection WinLDAP binds and searches on, as curl 8.22.0's Schannel build does (CGR-234 remainder from BL-3763).

## Context

CGR-234 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; BL-3763 fixed the bind failure text and split this out. Code: `LdapProtocolHandler.ConnectAndSearchAsync`. Decision: ADR-0978.

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The task was claimed with only the template's placeholder criterion, and the board refuses changed criteria at Done, so the checks this run met are listed here instead:
  - In the WinLDAP dialect without credentials, the handler connects twice to the same target; the first connection carries no bytes and is closed, the second carries the binds (`Cgr234ConnectionTests.ExecuteAsync_WinLdapWithoutCredentials_OpensAnEmptyConnectionBeforeTheWinLdapConnection`).
  - A failure to open WinLDAP's connection ends the transfer with that failure and closes the empty one.
  - With `-u`, and in the OpenLDAP dialect, there is one connection.
  - `Curl.Protocol.Ldap.UnitLibrary` meets the quality gates (Measure-CodeQuality: 0 failing members).

- Scope taken from the gap report: the extra connection is pinned only without `-u`, because the report's Windows re-run found that with `-u` the wire already matched. Real curl was not re-measured (no LDAP bench in this repository); recorded in ADR-0978.
- The verbose connection lines still name the first (curl's) connection; a default taken because curl's own connection is the one its verbose output describes.
- Tests are fake-connection unit tests, platform-neutral.
- Quality: the first measure found `ConnectAndSearchAsync` at complexity 12; the dialect choice moved to `SearchAsync`, after which 0 failing members, worst CRAP 10.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28: Doing -> Done. Without -u the WinLDAP dialect opens curl's empty connection before WinLDAP's own
