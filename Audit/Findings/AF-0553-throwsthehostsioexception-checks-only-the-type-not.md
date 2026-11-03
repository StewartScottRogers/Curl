---
id: AF-0553
title: ThrowsTheHostsIOException checks only the type, not that it is the host's own exception
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs:Start_WhenTheHostCannotStartIt_ThrowsTheHostsIOException:weak-assertion
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
# AF-0553 - ThrowsTheHostsIOException checks only the type, not that it is the host's own exception

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs:35`: ThrowsTheHostsIOException checks only the type, not that it is the host's own exception. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs:35`

The body is 'Assert.ThrowsExactly<IOException>(() => new ExtensionHelperProcessStarter(new StandInExtensionProcessStarter(null)).Start("p", "demo://h"));'. The stand-in throws new IOException("The system cannot find the file specified.") and nothing compares that message or instance, so a starter that threw its own IOException would pass. This is the same pattern as AF-0526.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Plugin.UnitTests/ExtensionHelperProcessStarterTests.cs -Pattern 'ThrowsTheHostsIOException' -Context 0,2
```

- Expected: The thrown exception's message, or identity, is asserted.
- Actual: Only Assert.ThrowsExactly<IOException>(...).

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
