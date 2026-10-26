---
id: AF-0450
title: Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Mcp.UnitTests/CurlMcpHostTests.cs:Build_OverTheProcess_BuildsAHost:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-25: filed proposed.
