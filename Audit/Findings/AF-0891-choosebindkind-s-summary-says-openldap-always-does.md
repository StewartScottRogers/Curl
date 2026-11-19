---
id: AF-0891
title: ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:ChooseBindKind:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-19: filed proposed.
