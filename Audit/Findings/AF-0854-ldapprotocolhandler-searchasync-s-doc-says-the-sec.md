---
id: AF-0854
title: LdapProtocolHandler.SearchAsync's doc says the second WinLDAP connection is only for the logged-on-user bind; the code uses it for every plaintext ldap:// and every credential-less transfer
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:SearchAsync:false-doc-comment
reproduction: none
task: BL-5374
tasks: BL-5374
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0854 - LdapProtocolHandler.SearchAsync's doc says the second WinLDAP connection is only for the logged-on-user bind; the code uses it for every plaintext ldap:// and every credential-less transfer

## Summary

Low finding from the truthfulness auditor at `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:175`: LdapProtocolHandler.SearchAsync's doc says the second WinLDAP connection is only for the logged-on-user bind; the code uses it for every plaintext ldap:// and every credential-less transfer. Reported by an auditor flagged unreliable in 2026-11-17_0430.md.

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:175`

Lines 174-175: 'Binds and searches on connection, curl's own connection, or, for the WinLDAP dialect's bind as the logged-on user, on a second connection WinLDAP opens beside it.' Line 178: 'dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls) ? SearchBesideEmptyConnectionAsync(...)'. So a plaintext ldap:// transfer with -u (a simple bind, not the logged-on user) also takes the second connection, as ADR-1280 decided ('for every plaintext ldap:// transfer, with or without -u'). The summary of SearchBesideEmptyConnectionAsync ('The WinLDAP dialect's bind as the logged-on user ... as curl 8.22.0's Schannel build does without -u') is stale in the same way. Both are true in substance for the old case only, so this is Low.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs -Pattern 'bind as the logged-on user, on a second|Credentials is null \|\| !target.UseTls' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: A doc comment naming every case the condition on line 178 covers (WinLDAP over plaintext ldap://, or without credentials)
- Actual: 175: /// dialect's bind as the logged-on user, on a second connection WinLDAP opens beside it. / 178: dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls)

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the reproduction: only '182: dialect == LdapDialect.WinLdap && (context.Credentials is null || !target.UseTls)' matches. The old 'bind as the logged-on user, on a second' phrase is gone. SearchAsync's summary (lines 176-179) now says the second WinLDAP connection is used over plaintext ldap:// (with or without -u) or without credentials, which matches the condition.

## Log

- 2026-11-17: filed proposed.
