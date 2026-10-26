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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
