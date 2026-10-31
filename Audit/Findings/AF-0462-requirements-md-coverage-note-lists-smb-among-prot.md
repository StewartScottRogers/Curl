---
id: AF-0462
title: Requirements.md coverage note lists SMB among protocols the code implements and registers, but smb/smbs are registered only as a refusing DisabledProtocolHandler
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Product/Requirements.md:Coverage-SMB-registered:false-statement
reproduction: none
task: BL-4224
tasks: BL-4224
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0462 - Requirements.md coverage note lists SMB among protocols the code implements and registers, but smb/smbs are registered only as a refusing DisabledProtocolHandler

## Summary

Low finding from the truthfulness auditor at `Documentation/Product/Requirements.md:11`: Requirements.md coverage note lists SMB among protocols the code implements and registers, but smb/smbs are registered only as a refusing DisabledProtocolHandler.

## Evidence

Location: `Documentation/Product/Requirements.md:11`

Requirements.md lines 7-11: "although the code implements much more: FTP and FTPS, ... ; SFTP, SCP and SMB are registered in `Curl.Console` too." Curl.Engine.UnitLibrary/EngineComposition.cs:177 registers `new LazyProtocolHandler(["smb", "smbs"], () => new DisabledProtocolHandler(["smb", "smbs"]))`, and DisabledProtocolHandler.ExecuteAsync (Curl.Engine.UnitLibrary/DisabledProtocolHandler.cs) fails every transfer with exit 1 and `Protocol "smb" is disabled`. SmbProtocolHandler is never constructed by the engine or Curl.Console. Curl.Engine.UnitLibrary/CLAUDE.md:534-536 describes this correctly, so the two documents disagree. Phase 2: explained by ADR-0835 (smb and smbs are disabled as in the reference builds); the Requirements note was not brought in line.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'SCP and SMB are registered', 'new DisabledProtocolHandler\(\["smb"'
```

- Expected: Requirements.md says SMB is registered only as disabled (exit 1), or does not list it among implemented protocols.
- Actual: Requirements.md:11: files; SFTP, SCP and SMB are registered in `Curl.Console` too. ...; EngineComposition.cs:177: new LazyProtocolHandler(["smb", "smbs"], () => new DisabledProtocolHandler(["smb", "smbs"])),

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
