---
id: BL-4400
title: Fix AF-0490: UseProtocolVersion_OnStdio_DoesNothing asserts only that the session it just made is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.McpClient.UnitTests]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4400 — Fix AF-0490: UseProtocolVersion_OnStdio_DoesNothing asserts only that the session it just made is not null

## Goal

The defect the audit office reported as AF-0490 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0490 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0490-useprotocolversion-onstdio-doesnothing-asserts-onl.md`.

Location: `Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs:187`

Location: `Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs:187`

The body builds a McpStdioClientSession, calls session.UseProtocolVersion("2025-06-18"), then Assert.IsNotNull(session);. That checks a local the test created, which can never be null, so the assertion can never fail. Nothing checks that the stdio session's state or the bytes sent to the server process are unchanged, which is what 'DoesNothing' promises.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs -Pattern 'UseProtocolVersion_OnStdio_DoesNothing' -Context 0,7
```

- Expected: The test asserts something UseProtocolVersion could change, e.g. that nothing was written to the in-memory server process.
- Actual: The only assertion is Assert.IsNotNull(session);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Pipeline `feature` run as a direct test-only change: the test now keeps the InMemoryMcpServerProcess and asserts nothing was written to its standard input and it was not closed, killed or disposed. The reproduction now shows `Assert.AreEqual(string.Empty, process.WrittenInput);` within its 7 context lines. Platform-neutral (in-memory streams only).
- dotnet build clean (0 warnings); fast tests green (0 failed); Curl.Extension.McpClient.UnitTests 184 passed. No production code changed, so no Measure-CodeQuality run.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-31: Doing -> Done. UseProtocolVersion_OnStdio_DoesNothing asserts the stdio server process saw no input and was not closed, killed or disposed
