---
id: AF-0834
title: Requirements.md says SMB is registered only as disabled, failing with exit 1; the engine serves smb/smbs
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-11-18_0810.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-17_0430.md, 2026-11-18_0810.md).
key: truthfulness:Documentation/Product/Requirements.md:SMB:false-statement
reproduction: none
task: BL-5308
tasks: BL-5308
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed: 2026-11-18
closed-how: consecutive
closed-by: 2026-11-17_0430.md, 2026-11-18_0810.md
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

- 2026-11-17 | 2026-11-17_0430.md | reproduces: no | Ran the reproduction: Documentation/Product/Requirements.md 'SMB is registered only' count 0; EngineComposition.cs 'new SmbProtocolHandler' count 1. Requirements.md:11-12 now says 'smb:// and smbs:// are served by the SMB handler, as in the OpenSSL reference build (ADR-1281)', which matches the code.
- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the reproduction: Requirements.md 'SMB is registered only' count 0, and 'new SmbProtocolHandler' count 1. Requirements.md:11-12 now says smb:// and smbs:// are served by the SMB handler (ADR-1281).

## Log

- 2026-11-16: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-17_0430.md, 2026-11-18_0810.md).
