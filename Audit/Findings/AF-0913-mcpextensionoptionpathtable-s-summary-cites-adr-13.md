---
id: AF-0913
title: McpExtensionOptionPathTable's summary cites ADR-1311 (socketio auth) for the decision ADR-1310 records
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs:McpExtensionOptionPathTable:false-doc-comment
reproduction: none
task: BL-5628
tasks: BL-5628
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_1740.md
---
# AF-0913 - McpExtensionOptionPathTable's summary cites ADR-1311 (socketio auth) for the decision ADR-1310 records

## Summary

Low finding from the truthfulness auditor at `Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs:6`: McpExtensionOptionPathTable's summary cites ADR-1311 (socketio auth) for the decision ADR-1310 records.

## Evidence

Location: `Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs:6`

The summary reads 'How the options the extensions register use a local path (BL-5564, ADR-1311)'. ADR-1311 is 'The socketio client sends -u as the CONNECT packet's auth payload' (BL-5567). The decision this class implements, McpExtensionOptionPathTable classifying extension options as path or pathless and failing closed, is ADR-1310 'The curl-mcp curl tool knows the extension options and confines those that name a path' (Task BL-5564). No file in Curl.Mcp* cites ADR-1310. A reader following the reference finds an unrelated ADR.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp.UnitLibrary/McpExtensionOptionPathTable.cs -Pattern 'ADR-13\d\d'; Select-String -Path Documentation/Planning/Decisions/ADR-1310-*.md,Documentation/Planning/Decisions/ADR-1311-*.md -Pattern '^# ADR'
```

- Expected: The summary cites ADR-1310, whose title is the extension-option path table decision.
- Actual: McpExtensionOptionPathTable.cs:6 cites ADR-1311, whose title is 'The socketio client sends -u as the CONNECT packet's auth payload'.

## Re-audits

- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | Ran the reproduction. McpExtensionOptionPathTable.cs:6 now reads 'How the options the extensions register use a local path (BL-5564, ADR-1310)', and no ADR-1311 citation remains. ADR-1310's title is 'The curl-mcp `curl` tool knows the extension options and confines those that name a path', which is the decision the summary describes. ADR-1311 is the socketio auth payload decision.

## Log

- 2026-11-20: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
