---
id: BL-4224
title: Fix AF-0462: Requirements.md coverage note lists SMB among protocols the code implements and registers, but smb/smbs are registered only as a refusing DisabledProtocolHandler
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4224 — Fix AF-0462: Requirements.md coverage note lists SMB among protocols the code implements and registers, but smb/smbs are registered only as a refusing DisabledProtocolHandler

## Goal

The defect the audit office reported as AF-0462 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0462 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0462-requirements-md-coverage-note-lists-smb-among-prot.md`.

Location: `Documentation/Product/Requirements.md:11`

Location: `Documentation/Product/Requirements.md:11`

Requirements.md lines 7-11: "although the code implements much more: FTP and FTPS, ... ; SFTP, SCP and SMB are registered in `Curl.Console` too." Curl.Engine.UnitLibrary/EngineComposition.cs:177 registers `new LazyProtocolHandler(["smb", "smbs"], () => new DisabledProtocolHandler(["smb", "smbs"]))`, and DisabledProtocolHandler.ExecuteAsync (Curl.Engine.UnitLibrary/DisabledProtocolHandler.cs) fails every transfer with exit 1 and `Protocol "smb" is disabled`. SmbProtocolHandler is never constructed by the engine or Curl.Console. Curl.Engine.UnitLibrary/CLAUDE.md:534-536 describes this correctly, so the two documents disagree. Phase 2: explained by ADR-0835 (smb and smbs are disabled as in the reference builds); the Requirements note was not brought in line.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'SCP and SMB are registered', 'new DisabledProtocolHandler\(\["smb"'
```

- Expected: Requirements.md says SMB is registered only as disabled (exit 1), or does not list it among implemented protocols.
- Actual: Requirements.md:11: files; SFTP, SCP and SMB are registered in `Curl.Console` too. ...; EngineComposition.cs:177: new LazyProtocolHandler(["smb", "smbs"], () => new DisabledProtocolHandler(["smb", "smbs"])),

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Requirements.md coverage note now lists only SFTP and SCP as registered, and says SMB is registered only as disabled (exit 1, `Protocol "<scheme>" is disabled`), citing ADR-0835. The reproduction no longer matches the Requirements line. Build clean, fast tests green (no Failed! project). Delegation to align-and-document skipped: a one-sentence edit costs less done directly.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. Requirements.md coverage note says SMB is registered only as disabled, matching ADR-0835 and the engine
