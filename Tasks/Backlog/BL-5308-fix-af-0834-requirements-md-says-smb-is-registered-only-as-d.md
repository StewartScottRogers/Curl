---
id: BL-5308
title: Fix AF-0834: Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-5308 — Fix AF-0834: Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs

## Goal

The defect the audit office reported as AF-0834 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0834 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Product/Requirements.md:11`

Location: `Documentation/Product/Requirements.md:11`

Requirements.md:11-13: 'SMB is registered only as disabled: `smb://` and `smbs://` URLs fail with exit 1 and `Protocol "<scheme>" is disabled`, as in curl 8.22.0's reference builds (ADR-0835).' The code registers the real handler: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))' (ADR-1281 supersedes ADR-0835).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Product/Requirements.md -Pattern 'SMB is registered only').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0, 1: the requirements describe smb/smbs as served.
- Actual: 1, 1: the requirements say registered only as disabled while the composition serves them.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
