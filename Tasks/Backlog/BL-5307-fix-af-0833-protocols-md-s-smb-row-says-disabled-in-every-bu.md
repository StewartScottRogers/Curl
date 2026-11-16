---
id: BL-5307
title: Fix AF-0833: Protocols.md's SMB row says disabled in every build, exit 1, not in curl -V; the code serves smb/smbs and lists them
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Protocols.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-5307 — Fix AF-0833: Protocols.md's SMB row says disabled in every build, exit 1, not in curl -V; the code serves smb/smbs and lists them

## Goal

The defect the audit office reported as AF-0833 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0833 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Protocols.md:35`

Location: `Documentation/Protocols.md:35`

Protocols.md:35, SMB row: 'Disabled in every build, as in curl 8.22.0: a transfer fails with exit 1 and `Protocol "smb" is disabled`, and `curl -V` does not list them (ADR-0835, ADR-1263). The library holds the SMBv1 client a build with SMB would use.' The code: Curl.Engine.UnitLibrary/EngineComposition.cs:197 registers SmbProtocolHandler for ["smb", "smbs"]; ManagedCurlVersion.cs:18 lists smb and smbs in -V (commit e019d9e5a8, BL-5286, ADR-1281).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Protocols.md -Pattern 'Disabled in every build').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0, 1: the SMB row says the client serves smb and smbs.
- Actual: 1, 1: the row says disabled while the composition serves them.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
