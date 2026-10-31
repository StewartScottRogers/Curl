---
id: AF-0490
title: UseProtocolVersion_OnStdio_DoesNothing asserts only that the session it just made is not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs:UseProtocolVersion_OnStdio_DoesNothing:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0490 - UseProtocolVersion_OnStdio_DoesNothing asserts only that the session it just made is not null

## Summary

Low finding from the quality auditor at `Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs:187`: UseProtocolVersion_OnStdio_DoesNothing asserts only that the session it just made is not null.

## Evidence

Location: `Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs:187`

The body builds a McpStdioClientSession, calls session.UseProtocolVersion("2025-06-18"), then Assert.IsNotNull(session);. That checks a local the test created, which can never be null, so the assertion can never fail. Nothing checks that the stdio session's state or the bytes sent to the server process are unchanged, which is what 'DoesNothing' promises.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.McpClient.UnitTests/Client/McpProtocolHandlerTests.cs -Pattern 'UseProtocolVersion_OnStdio_DoesNothing' -Context 0,7
```

- Expected: The test asserts something UseProtocolVersion could change, e.g. that nothing was written to the in-memory server process.
- Actual: The only assertion is Assert.IsNotNull(session);

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the Select-String: after session.UseProtocolVersion("2025-06-18") the test asserts Assert.AreEqual(string.Empty, process.WrittenInput) (line 188), not just that the session is non-null.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
