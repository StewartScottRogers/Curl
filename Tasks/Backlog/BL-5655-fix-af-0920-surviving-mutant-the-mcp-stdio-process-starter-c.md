---
id: BL-5655
title: Fix AF-0920: Surviving mutant: the MCP stdio process starter can set UseShellExecute, which cannot redirect the server's streams
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5655 — Fix AF-0920: Surviving mutant: the MCP stdio process starter can set UseShellExecute, which cannot redirect the server's streams

## Goal

The defect the audit office reported as AF-0920 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0920 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26`

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs:26`

`UseShellExecute = false,` mutated to `true` survived Curl.Extended.Protocol.McpClient.UnitTests. With RedirectStandardInput/Output set, Process.Start then throws InvalidOperationException, so every mcp+stdio:// transfer fails to start its server (exit 7). The class is [ExcludeFromCodeCoverage] as a thin Process adapter (ADR-1031), so no test constructs its ProcessStartInfo; a test of the start-info it builds, without starting a process, would pin it.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs line 26` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs line 26`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
