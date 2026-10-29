---
id: AF-0450
title: Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
key: quality:Curl.Mcp.UnitTests/CurlMcpHostTests.cs:Build_OverTheProcess_BuildsAHost:weak-assertion
reproduction: none
task: BL-3527
tasks: BL-3527
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed: 2026-10-29
closed-how: consecutive
closed-by: 2026-10-25_1944.md, 2026-10-29_0606.md
---
# AF-0450 - Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null

## Summary

Low finding from the quality auditor at `Curl.Mcp.UnitTests/CurlMcpHostTests.cs:88`: Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Mcp.UnitTests/CurlMcpHostTests.cs:88`

The body is: using McpStdioHost host = CurlMcpHost.Build(); Assert.IsNotNull(host);. A factory that returns new cannot give null, so the assertion cannot fail. Nothing checks that the host is wired to the process's standard streams or registers Curl's tools.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp.UnitTests/CurlMcpHostTests.cs -Pattern 'Build_OverTheProcess_BuildsAHost' -Context 0,5
```

- Expected: The test asserts something Build configures (its tools or its streams).
- Actual: The only assertion is Assert.IsNotNull(host);

## Re-audits

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Select-String finds no Build_OverTheProcess_BuildsAHost. It is now Build_OverTheProcess_ReadsTheResponseCapFromTheProcessEnvironment (CurlMcpHostTests.cs:84). It sets the cap variable to 'lots' and asserts that RunAsync throws McpConfigurationException with a message naming the variable.
- 2026-10-29 | 2026-10-29_0606.md | reproduces: no | Ran the Select-String reproduction: no match. The test is now Build_OverTheProcess_ReadsTheResponseCapFromTheProcessEnvironment (CurlMcpHostTests.cs:84), which asserts a McpConfigurationException naming the response-cap variable. Build_ServesInitializeAndToolsListOnOutputAndLogsOnlyToError asserts exit 0 and the served serverInfo and tool list.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-29: accepted -> closed. Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
