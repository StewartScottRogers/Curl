---
id: AF-0853
title: ADR-0835 is still Accepted and says EngineComposition serves smb/smbs with a DisabledProtocolHandler (exit 1); ADR-1281 superseded it and the engine serves SMB
auditor: truthfulness
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md:ADR-0835-Status:stale-adr
reproduction: none
task: BL-5373
tasks: BL-5373
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0853 - ADR-0835 is still Accepted and says EngineComposition serves smb/smbs with a DisabledProtocolHandler (exit 1); ADR-1281 superseded it and the engine serves SMB

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md:3`: ADR-0835 is still Accepted and says EngineComposition serves smb/smbs with a DisabledProtocolHandler (exit 1); ADR-1281 superseded it and the engine serves SMB. Reported by an auditor flagged unreliable in 2026-11-17_0430.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0835-smb-and-smbs-are-disabled-as-in-the-reference-builds.md:3`

ADR-0835 says '- **Status:** Accepted' (line 3). Its Decision line 28 says '`EngineComposition.CreateProtocolHandlers` serves `smb` and `smbs` with a `DisabledProtocolHandler`, which fails every transfer with exit 1 and `Protocol "<scheme>" is disabled`'. ADR-1281 ('Supersedes ADR-0835') says 'EngineComposition registers SmbProtocolHandler for smb and smbs; DisabledProtocolHandler is removed', and EngineComposition.cs:197 registers SmbProtocolHandler. ADR-0835's status was not changed to Superseded, so it states a wrong exit code (1) and a wrong handler for a scheme the code now serves.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0835-*.md -Pattern 'Status:\*\* Accepted').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0 then 1: ADR-0835 marked superseded by ADR-1281
- Actual: 1 then 1: ADR-0835 still Accepted while the engine serves SMB

## Re-audits

## Log

- 2026-11-17: filed proposed.
