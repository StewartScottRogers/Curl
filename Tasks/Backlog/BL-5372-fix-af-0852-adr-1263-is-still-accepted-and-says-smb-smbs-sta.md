---
id: BL-5372
title: Fix AF-0852: ADR-1263 is still Accepted and says smb/smbs stay disabled in every build, but the engine serves them (superseded by ADR-1281)
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-5372 — Fix AF-0852: ADR-1263 is still Accepted and says smb/smbs stay disabled in every build, but the engine serves them (superseded by ADR-1281)

## Goal

The defect the audit office reported as AF-0852 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0852 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md:3`

Location: `Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md:3`

ADR-1263 says '- **Status:** Accepted' (line 3), and its Decision item 1 says '`smb` and `smbs` are disabled in all builds, on every OS ... No code changes.' Item 2 also says Protocols.md gives exit 1 for SMB. The code: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))', and ManagedCurlVersion.cs:18 lists "smb", "smbs". README.md:24-26 and Documentation/Protocols.md:35 both say ADR-1281 'supersedes ADR-0835 and ADR-1263'. However, ADR-1263 is not marked superseded, and ADR-1281 itself names only ADR-0835. An agent reading the Decisions folder still finds an Accepted ADR ordering SMB disabled. Its own line 34 says a later decision to serve SMB supersedes it, but the status was never updated.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-1263-*.md -Pattern 'Status:\*\* Accepted').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0 then 1: ADR-1263 marked superseded by ADR-1281 while the engine serves SMB
- Actual: 1 then 1: ADR-1263 still Accepted while the engine serves SMB

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
