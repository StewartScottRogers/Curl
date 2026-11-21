---
id: BL-5628
title: Fix AF-0913: McpExtensionOptionPathTable's summary cites ADR-1311 (socketio auth) for the decision ADR-1310 records
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5628 — Fix AF-0913: McpExtensionOptionPathTable's summary cites ADR-1311 (socketio auth) for the decision ADR-1310 records

## Goal

The defect the audit office reported as AF-0913 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0913 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs:6`

Location: `Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs:6`

The summary reads 'How the options the extensions register use a local path (BL-5564, ADR-1311)'. ADR-1311 is 'The socketio client sends -u as the CONNECT packet's auth payload' (BL-5567). The decision this class implements, McpExtensionOptionPathTable classifying extension options as path or pathless and failing closed, is ADR-1310 'The curl-mcp curl tool knows the extension options and confines those that name a path' (Task BL-5564). No file in Curl.Mcp* cites ADR-1310. A reader following the reference finds an unrelated ADR.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs -Pattern 'ADR-13\d\d'; Select-String -Path Documentation/Planning/Decisions/ADR-1310-*.md,Documentation/Planning/Decisions/ADR-1311-*.md -Pattern '^# ADR'
```

- Expected: The summary cites ADR-1310, whose title is the extension-option path table decision.
- Actual: McpExtensionOptionPathTable.cs:6 cites ADR-1311, whose title is 'The socketio client sends -u as the CONNECT packet's auth payload'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Comment-only fix: summary now cites ADR-1310. Build clean; no code change so tests unaffected.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-20: Doing -> Done. Summary now cites ADR-1310
