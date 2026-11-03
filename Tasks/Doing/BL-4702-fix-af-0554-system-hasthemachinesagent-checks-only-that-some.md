---
id: BL-4702
title: Fix AF-0554: System_HasTheMachinesAgent checks only that some agent connector exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SshJump.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4702 — Fix AF-0554: System_HasTheMachinesAgent checks only that some agent connector exists

## Goal

The defect the audit office reported as AF-0554 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0554 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0554-system-hasthemachinesagent-checks-only-that-some-a.md`.

Location: `Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs:28`

Location: `Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs:28`

The whole test is 'Assert.IsNotNull(SshJumpServices.System.AgentConnector);'. The name promises the machine's agent (PlatformSshAgentConnector.Create(Environment.GetEnvironmentVariable, ...)), yet any connector passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs -Pattern 'System_HasTheMachinesAgent' -Context 0,1
```

- Expected: An assertion on the connector's type or platform choice.
- Actual: Assert.IsNotNull(SshJumpServices.System.AgentConnector);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test-only change: System_HasTheMachinesAgent now asserts the platform choice (Pageant then system on Windows, system alone elsewhere). Build and SshJump tests green; no production code changed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
