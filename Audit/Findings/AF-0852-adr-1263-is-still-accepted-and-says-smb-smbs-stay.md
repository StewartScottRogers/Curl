---
id: AF-0852
title: ADR-1263 is still Accepted and says smb/smbs stay disabled in every build, but the engine serves them (superseded by ADR-1281)
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md:ADR-1263-Status:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0852 - ADR-1263 is still Accepted and says smb/smbs stay disabled in every build, but the engine serves them (superseded by ADR-1281)

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md:3`: ADR-1263 is still Accepted and says smb/smbs stay disabled in every build, but the engine serves them (superseded by ADR-1281). Reported by an auditor flagged unreliable in 2026-11-17_0430.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1263-smb-and-smbs-stay-disabled-in-every-build-and-the-docs-say-so.md:3`

ADR-1263 says '- **Status:** Accepted' (line 3), and its Decision item 1 says '`smb` and `smbs` are disabled in all builds, on every OS ... No code changes.' Item 2 also says Protocols.md gives exit 1 for SMB. The code: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))', and ManagedCurlVersion.cs:18 lists "smb", "smbs". README.md:24-26 and Documentation/Protocols.md:35 both say ADR-1281 'supersedes ADR-0835 and ADR-1263'. However, ADR-1263 is not marked superseded, and ADR-1281 itself names only ADR-0835. An agent reading the Decisions folder still finds an Accepted ADR ordering SMB disabled. Its own line 34 says a later decision to serve SMB supersedes it, but the status was never updated.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-1263-*.md -Pattern 'Status:\*\* Accepted').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0 then 1: ADR-1263 marked superseded by ADR-1281 while the engine serves SMB
- Actual: 1 then 1: ADR-1263 still Accepted while the engine serves SMB

## Re-audits

## Log

- 2026-11-17: filed proposed.
