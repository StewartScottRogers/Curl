---
id: BL-5563
title: Fix AF-0892: LdapBindKind's summary says it is how the WinLDAP dialect binds, but it also holds the OpenLDAP dialect's Sasl and Simple binds
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5563 — Fix AF-0892: LdapBindKind's summary says it is how the WinLDAP dialect binds, but it also holds the OpenLDAP dialect's Sasl and Simple binds

## Goal

The defect the audit office reported as AF-0892 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0892 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs:4`

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs:4`

Type summary line 4: 'How the WinLDAP dialect binds, as curl's ldap_win_bind and ldap_win_bind_auth choose'. Member Sasl (line 21) is documented 'The OpenLDAP dialect's SASL bind', and LdapProtocolHandler.ChooseOpenLdapBindKind returns Simple or Sasl for the OpenLDAP dialect, so the enum serves both dialects.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKind.cs -Pattern 'How the WinLDAP dialect binds|OpenLDAP dialect.s SASL bind' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: Only line 21 matches; the type summary names both dialects.
- Actual: 4: /// How the WinLDAP dialect binds, as curl's <c>ldap_win_bind</c> and <c>ldap_win_bind_auth</c>
21: /// <summary>The OpenLDAP dialect's SASL bind, chosen by the auth mask or <c>AUTH=</c> login options (BL-5435).</summary>

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Doc-only summary change; built the library clean, no behaviour touched.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-19: Doing -> Done. LdapBindKind summary names both dialects
