---
id: AF-0915
title: Surviving mutant: the MCP event-stream reader can drop a line's LF that arrives in the next read and merge two lines
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:ReadLineAsync-false:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75:false
task: BL-5650
tasks: BL-5650
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_0114.md
---
# AF-0915 - Surviving mutant: the MCP event-stream reader can drop a line's LF that arrives in the next read and merge two lines

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75`: Surviving mutant: the MCP event-stream reader can drop a line's LF that arrives in the next read and merge two lines.

## Evidence

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75`

In ReadLineAsync, `skipLineFeed = false;` (line 75, right after the leading-LF skip) mutated to `skipLineFeed = true;` survived Curl.Extended.Protocol.McpClient.UnitTests. When a line has no terminator yet in the buffer, the loop fills again; with the mutant the next read's leading '\n' is skipped as if it followed a CR, so a bare-LF-terminated line split before its LF is joined to the next line and the SSE 'data:' field, and with it the JSON-RPC response and the tool output written to stdout, changes. ReadEventDataAsync_CrLfSplitAcrossReads_IsOneLineEnd covers only a CR|LF split, not a text|LF split.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75:false -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_0114.md | reproduces: no | Ran the -Site .../McpEventStreamReader.cs:75:false command: resolvedLine 75, 'skipLineFeed = false;' -> 'skipLineFeed = true;', outcome killed, no excluded tests. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
