---
id: BL-5373
title: Fix AF-0853: ADR-0835 is still Accepted and says EngineComposition serves smb/smbs with a DisabledProtocolHandler (exit 1); ADR-1281 superseded it and the engine serves SMB
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5373 — Fix AF-0853: ADR-0835 is still Accepted and says EngineComposition serves smb/smbs with a DisabledProtocolHandler (exit 1); ADR-1281 superseded it and the engine serves SMB

## Goal

The defect the audit office reported as AF-0853 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0853 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md:3`

Location: `Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md:3`

ADR-0835 says '- **Status:** Accepted' (line 3). Its Decision line 28 says '`EngineComposition.CreateProtocolHandlers` serves `smb` and `smbs` with a `DisabledProtocolHandler`, which fails every transfer with exit 1 and `Protocol "<scheme>" is disabled`'. ADR-1281 ('Supersedes ADR-0835') says 'EngineComposition registers SmbProtocolHandler for smb and smbs; DisabledProtocolHandler is removed', and EngineComposition.cs:197 registers SmbProtocolHandler. ADR-0835's status was not changed to Superseded, so it states a wrong exit code (1) and a wrong handler for a scheme the code now serves.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0835-*.md -Pattern 'Status:\*\* Accepted').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0 then 1: ADR-0835 marked superseded by ADR-1281
- Actual: 1 then 1: ADR-0835 still Accepted while the engine serves SMB

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- ADR-0835 status set to Superseded by ADR-1281 and its row in the ADR README updated. Reproduction gives 0 then 1. Build clean, fast tests green.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-17: Doing -> Done. ADR-0835 marked Superseded by ADR-1281
