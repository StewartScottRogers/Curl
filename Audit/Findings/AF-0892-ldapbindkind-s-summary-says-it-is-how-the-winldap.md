---
id: AF-0892
title: LdapBindKind's summary says it is how the WinLDAP dialect binds, but it also holds the OpenLDAP dialect's Sasl and Simple binds
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs:LdapBindKind:false-doc-comment
reproduction: none
task: BL-5563
tasks: BL-5563
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_1633.md
---
# AF-0892 - LdapBindKind's summary says it is how the WinLDAP dialect binds, but it also holds the OpenLDAP dialect's Sasl and Simple binds

## Summary

Low finding from the truthfulness auditor at `Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs:4`: LdapBindKind's summary says it is how the WinLDAP dialect binds, but it also holds the OpenLDAP dialect's Sasl and Simple binds.

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs:4`

Type summary line 4: 'How the WinLDAP dialect binds, as curl's ldap_win_bind and ldap_win_bind_auth choose'. Member Sasl (line 21) is documented 'The OpenLDAP dialect's SASL bind', and LdapProtocolHandler.ChooseOpenLdapBindKind returns Simple or Sasl for the OpenLDAP dialect, so the enum serves both dialects.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs -Pattern 'How the WinLDAP dialect binds|OpenLDAP dialect.s SASL bind' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: Only line 21 matches; the type summary names both dialects.
- Actual: 4: /// How the WinLDAP dialect binds, as curl's <c>ldap_win_bind</c> and <c>ldap_win_bind_auth</c>
21: /// <summary>The OpenLDAP dialect's SASL bind, chosen by the auth mask or <c>AUTH=</c> login options (BL-5435).</summary>

## Re-audits

- 2026-11-19 | 2026-11-19_1633.md | reproduces: no | Ran the reproduction. It matched only "22: /// <summary>The OpenLDAP dialect's SASL bind, chosen by the auth mask or <c>AUTH=</c> login options (BL-5435).</summary>". The text 'How the WinLDAP dialect binds' is gone. The enum summary now reads 'How a bind is made, in either dialect: the WinLDAP dialect as curl's ldap_win_bind ... the OpenLDAP dialect as ChooseOpenLdapBindKind chooses (Simple or Sasl)'.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces.
