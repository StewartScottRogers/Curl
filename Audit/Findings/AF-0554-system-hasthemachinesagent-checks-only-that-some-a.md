---
id: AF-0554
title: System_HasTheMachinesAgent checks only that some agent connector exists
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs:System_HasTheMachinesAgent:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0554 - System_HasTheMachinesAgent checks only that some agent connector exists

## Summary

Low finding from the quality auditor at `Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs:28`: System_HasTheMachinesAgent checks only that some agent connector exists. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs:28`

The whole test is 'Assert.IsNotNull(SshJumpServices.System.AgentConnector);'. The name promises the machine's agent (PlatformSshAgentConnector.Create(Environment.GetEnvironmentVariable, ...)), yet any connector passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.SshJump.UnitTests/SshJumpServicesTests.cs -Pattern 'System_HasTheMachinesAgent' -Context 0,1
```

- Expected: An assertion on the connector's type or platform choice.
- Actual: Assert.IsNotNull(SshJumpServices.System.AgentConnector);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
