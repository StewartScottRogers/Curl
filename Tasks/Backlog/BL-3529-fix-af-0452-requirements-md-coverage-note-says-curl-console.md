---
id: BL-3529
title: Fix AF-0452: Requirements.md coverage note says Curl.Console does not register SFTP, SCP and SMB, but they are registered
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-25
completed:
---
# BL-3529 — Fix AF-0452: Requirements.md coverage note says Curl.Console does not register SFTP, SCP and SMB, but they are registered

## Goal

The defect the audit office reported as AF-0452 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0452 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0452-requirements-md-coverage-note-says-curl-console-do.md`.

Location: `Documentation/Product/Requirements.md:11`

Location: `Documentation/Product/Requirements.md:11`

Requirements.md lines 11-12 (the Coverage note): "SFTP, SCP and SMB have handler libraries that `Curl.Console` does not register (tasks BL-576, BL-598)." The code registers all three: Curl.Engine.UnitLibrary/EngineComposition.cs:176 `new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector)),` and :177 `new LazyProtocolHandler(["scp", "sftp"], () => new SshProtocolHandler(...`, inside CreateProtocolHandlers. Curl.Console/CurlComposition.cs:439 (and 512, 580, 617) builds its ProtocolDispatcher from CreateProtocolHandlers, and Curl.Console.csproj references Curl.Protocol.Smb.UnitLibrary and Curl.Protocol.Ssh.UnitLibrary. An agent reading the requirements would take scp://, sftp:// and smb:// as unsupported by the executable, and could file or block work to register handlers that are already registered.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'SCP and SMB have handler libraries that', 'LazyProtocolHandler\(\["(smb|scp)"' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only EngineComposition.cs lines (176, 177): the requirements no longer claim the handlers are unregistered.
- Actual: Requirements.md:11, EngineComposition.cs:176, EngineComposition.cs:177

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
