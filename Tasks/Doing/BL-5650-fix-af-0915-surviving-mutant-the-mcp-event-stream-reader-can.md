---
id: BL-5650
title: Fix AF-0915: Surviving mutant: the MCP event-stream reader can drop a line's LF that arrives in the next read and merge two lines
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5650 — Fix AF-0915: Surviving mutant: the MCP event-stream reader can drop a line's LF that arrives in the next read and merge two lines

## Goal

The defect the audit office reported as AF-0915 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0915 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75`

Location: `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs:75`

In ReadLineAsync, `skipLineFeed = false;` (line 75, right after the leading-LF skip) mutated to `skipLineFeed = true;` survived Curl.Extended.Protocol.McpClient.UnitTests. When a line has no terminator yet in the buffer, the loop fills again; with the mutant the next read's leading '\n' is skipped as if it followed a CR, so a bare-LF-terminated line split before its LF is joined to the next line and the SSE 'data:' field, and with it the JSON-RPC response and the tool output written to stdout, changes. ReadEventDataAsync_CrLfSplitAcrossReads_IsOneLineEnd covers only a CR|LF split, not a text|LF split.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs line 75` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpEventStreamReader.cs line 75`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 27daa311 9742157d.
