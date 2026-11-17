---
id: BL-5374
title: Fix AF-0854: LdapProtocolHandler.SearchAsync's doc says the second WinLDAP connection is only for the logged-on-user bind; the code uses it for every plaintext ldap:// and every credential-less transfer
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5374 — Fix AF-0854: LdapProtocolHandler.SearchAsync's doc says the second WinLDAP connection is only for the logged-on-user bind; the code uses it for every plaintext ldap:// and every credential-less transfer

## Goal

The defect the audit office reported as AF-0854 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0854 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:175`

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:175`

Lines 174-175: 'Binds and searches on connection, curl's own connection, or, for the WinLDAP dialect's bind as the logged-on user, on a second connection WinLDAP opens beside it.' Line 178: 'dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls) ? SearchBesideEmptyConnectionAsync(...)'. So a plaintext ldap:// transfer with -u (a simple bind, not the logged-on user) also takes the second connection, as ADR-1280 decided ('for every plaintext ldap:// transfer, with or without -u'). The summary of SearchBesideEmptyConnectionAsync ('The WinLDAP dialect's bind as the logged-on user ... as curl 8.22.0's Schannel build does without -u') is stale in the same way. Both are true in substance for the old case only, so this is Low.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs -Pattern 'bind as the logged-on user, on a second|Credentials is null \|\| !target.UseTls' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: A doc comment naming every case the condition on line 178 covers (WinLDAP over plaintext ldap://, or without credentials)
- Actual: 175: /// dialect's bind as the logged-on user, on a second connection WinLDAP opens beside it. / 178: dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Doc comments on SearchAsync and SearchBesideEmptyConnectionAsync now name plaintext ldap:// (with or without -u) and credential-less transfers. Comment-only change; build clean.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
