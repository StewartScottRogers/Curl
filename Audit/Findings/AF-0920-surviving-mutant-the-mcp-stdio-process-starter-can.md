---
id: AF-0920
title: Surviving mutant: the MCP stdio process starter can set UseShellExecute, which cannot redirect the server's streams
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:Start-false:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26:false
task: none
tasks:
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0920 - Surviving mutant: the MCP stdio process starter can set UseShellExecute, which cannot redirect the server's streams

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26`: Surviving mutant: the MCP stdio process starter can set UseShellExecute, which cannot redirect the server's streams.

## Evidence

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26`

`UseShellExecute = false,` mutated to `true` survived Curl.Extended.Protocol.McpClient.UnitTests. With RedirectStandardInput/Output set, Process.Start then throws InvalidOperationException, so every mcp+stdio:// transfer fails to start its server (exit 7). The class is [ExcludeFromCodeCoverage] as a thin Process adapter (ADR-1031), so no test constructs its ProcessStartInfo; a test of the start-info it builds, without starting a process, would pin it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26:false -Member Start -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
