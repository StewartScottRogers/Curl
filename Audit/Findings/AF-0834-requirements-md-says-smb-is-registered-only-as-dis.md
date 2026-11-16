---
id: AF-0834
title: Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs
auditor: truthfulness
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Documentation/Product/Requirements.md:SMB:false-statement
reproduction: none
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0834 - Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs

## Summary

High finding from the truthfulness auditor at `Documentation/Product/Requirements.md:11`: Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs.

## Evidence

Location: `Documentation/Product/Requirements.md:11`

Requirements.md:11-13: 'SMB is registered only as disabled: `smb://` and `smbs://` URLs fail with exit 1 and `Protocol "<scheme>" is disabled`, as in curl 8.22.0's reference builds (ADR-0835).' The code registers the real handler: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))' (ADR-1281 supersedes ADR-0835).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Product/Requirements.md -Pattern 'SMB is registered only').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0, 1: the requirements describe smb/smbs as served.
- Actual: 1, 1: the requirements say registered only as disabled while the composition serves them.

## Re-audits

## Log

- 2026-11-16: filed proposed.
