---
id: AF-0891
title: ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:ChooseBindKind:false-doc-comment
reproduction: none
task: BL-5562
tasks: BL-5562
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_1633.md
---
# AF-0891 - ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic

## Summary

Medium finding from the truthfulness auditor at `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:344`: ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic.

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:344`

Summary line 344: 'How the transfer binds: on OpenLDAP a simple bind; on WinLDAP what LdapBindKindChoice picks'. Body line 347-350 sends the OpenLDAP dialect to ChooseOpenLdapBindKind, which (lines 353-357) returns LdapBindKind.Sasl when credentials are given and LdapOpenLdapSaslBind.HasAuthOption(login options) is true or the auth mask lacks Basic, otherwise Simple. Phase 2 annotation: ADR-1299 decided this SASL choice; the comment kept the pre-ADR-1299 behaviour.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs -Pattern 'on OpenLDAP a simple bind|\? LdapBindKind\.Sasl' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: Only line 356 (? LdapBindKind.Sasl) matches; the summary says OpenLDAP binds with SASL under AUTH= or a mask without Basic.
- Actual: 344: /// How the transfer binds: on OpenLDAP a simple bind; on WinLDAP what <see cref="LdapBindKindChoice" />
356: ? LdapBindKind.Sasl

## Re-audits

- 2026-11-19 | 2026-11-19_1633.md | reproduces: no | Ran the reproduction. It matched only '357: ? LdapBindKind.Sasl'. The phrase 'on OpenLDAP a simple bind' no longer appears. ChooseBindKind's summary (LdapProtocolHandler.cs:343-347) now reads 'on OpenLDAP SASL with credentials when AUTH= login options are given or the auth mask lacks Basic, otherwise a simple bind (ADR-1299)'. That matches ChooseOpenLdapBindKind (lines 354-358), which returns Sasl for credentials plus (HasAuthOption || mask without Basic), and Simple otherwise.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces.
