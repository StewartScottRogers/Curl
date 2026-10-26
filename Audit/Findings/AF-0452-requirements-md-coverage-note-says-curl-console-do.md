---
id: AF-0452
title: Requirements.md coverage note says Curl.Console does not register SFTP, SCP and SMB, but they are registered
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Documentation/Product/Requirements.md:Coverage-SftpScpSmbUnregistered:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0452 - Requirements.md coverage note says Curl.Console does not register SFTP, SCP and SMB, but they are registered

## Summary

High finding from the truthfulness auditor at `Documentation/Product/Requirements.md:11`: Requirements.md coverage note says Curl.Console does not register SFTP, SCP and SMB, but they are registered. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Documentation/Product/Requirements.md:11`

Requirements.md lines 11-12 (the Coverage note): "SFTP, SCP and SMB have handler libraries that `Curl.Console` does not register (tasks BL-576, BL-598)." The code registers all three: Curl.Engine.UnitLibrary/EngineComposition.cs:176 `new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector)),` and :177 `new LazyProtocolHandler(["scp", "sftp"], () => new SshProtocolHandler(...`, inside CreateProtocolHandlers. Curl.Console/CurlComposition.cs:439 (and 512, 580, 617) builds its ProtocolDispatcher from CreateProtocolHandlers, and Curl.Console.csproj references Curl.Protocol.Smb.UnitLibrary and Curl.Protocol.Ssh.UnitLibrary. An agent reading the requirements would take scp://, sftp:// and smb:// as unsupported by the executable, and could file or block work to register handlers that are already registered.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'SCP and SMB have handler libraries that', 'LazyProtocolHandler\(\["(smb|scp)"' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only EngineComposition.cs lines (176, 177): the requirements no longer claim the handlers are unregistered.
- Actual: Requirements.md:11, EngineComposition.cs:176, EngineComposition.cs:177

## Re-audits

## Log

- 2026-10-25: filed proposed.
