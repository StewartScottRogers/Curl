---
id: BL-5562
title: Fix AF-0891: ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5562 — Fix AF-0891: ChooseBindKind's summary says OpenLDAP always does a simple bind; it picks SASL for AUTH= options or a mask without Basic

## Goal

The defect the audit office reported as AF-0891 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0891 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:344`

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs:344`

Summary line 344: 'How the transfer binds: on OpenLDAP a simple bind; on WinLDAP what LdapBindKindChoice picks'. Body line 347-350 sends the OpenLDAP dialect to ChooseOpenLdapBindKind, which (lines 353-357) returns LdapBindKind.Sasl when credentials are given and LdapOpenLdapSaslBind.HasAuthOption(login options) is true or the auth mask lacks Basic, otherwise Simple. Phase 2 annotation: ADR-1299 decided this SASL choice; the comment kept the pre-ADR-1299 behaviour.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs -Pattern 'on OpenLDAP a simple bind|\? LdapBindKind\.Sasl' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: Only line 356 (? LdapBindKind.Sasl) matches; the summary says OpenLDAP binds with SASL under AUTH= or a mask without Basic.
- Actual: 344: /// How the transfer binds: on OpenLDAP a simple bind; on WinLDAP what <see cref="LdapBindKindChoice" />
356: ? LdapBindKind.Sasl

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
