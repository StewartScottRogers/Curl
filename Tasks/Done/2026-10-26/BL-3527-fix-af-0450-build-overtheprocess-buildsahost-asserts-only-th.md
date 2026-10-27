---
id: BL-3527
title: Fix AF-0450: Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3527 — Fix AF-0450: Build_OverTheProcess_BuildsAHost asserts only that CurlMcpHost.Build() is not null

## Goal

The defect the audit office reported as AF-0450 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0450 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0450-build-overtheprocess-buildsahost-asserts-only-that.md`.

Location: `Curl.Mcp.UnitTests/CurlMcpHostTests.cs:88`

Location: `Curl.Mcp.UnitTests/CurlMcpHostTests.cs:88`

The body is: using McpStdioHost host = CurlMcpHost.Build(); Assert.IsNotNull(host);. A factory that returns new cannot give null, so the assertion cannot fail. Nothing checks that the host is wired to the process's standard streams or registers Curl's tools.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp.UnitTests/CurlMcpHostTests.cs -Pattern 'Build_OverTheProcess_BuildsAHost' -Context 0,5
```

- Expected: The test asserts something Build configures (its tools or its streams).
- Actual: The only assertion is Assert.IsNotNull(host);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Pipeline `feature` run directly: a one-test change in Curl.Mcp.UnitTests, no production code, so no plan, no quality run.
- The test is renamed `Build_OverTheProcess_ReadsTheResponseCapFromTheProcessEnvironment` (the old name only claimed a host was built). It sets the real process variable `CURL_MCP_MAX_RESPONSE_BYTES` to `lots`, calls the parameterless `Build()`, and asserts `RunAsync` refuses with `McpConfigurationException` naming the variable: proof that `Build()` wires the host to `Environment.GetEnvironmentVariable`. It restores the variable in `finally`; the class is `[DoNotParallelize]` and no other unit test reads the real variable. The throw comes before the host touches standard input, so the test never reads the real stdio (Curl.Mcp.UnitTests CLAUDE.md).
- Reproduction: the old pattern now finds nothing; `Select-String -Pattern Build_OverTheProcess -Context 0,16` shows the test asserting the refusal and its message, not `Assert.IsNotNull`.
- `dotnet build` clean; fast tests 0 failed across all test projects (Curl.Mcp.UnitTests 528 passed). Platform-neutral: no path, OS text or certificate.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. Build_OverTheProcess test now asserts Build() reads CURL_MCP_MAX_RESPONSE_BYTES from the process environment
