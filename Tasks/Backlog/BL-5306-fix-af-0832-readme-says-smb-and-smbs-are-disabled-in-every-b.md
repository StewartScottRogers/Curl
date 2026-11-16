---
id: BL-5306
title: Fix AF-0832: README says smb and smbs are disabled in every build and absent from curl -V, but Curl serves and lists them
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-5306 — Fix AF-0832: README says smb and smbs are disabled in every build and absent from curl -V, but Curl serves and lists them

## Goal

The defect the audit office reported as AF-0832 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0832 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `README.md:23`

Location: `README.md:23`

README.md:23-26: '`smb` and `smbs` are known but disabled in every build: a transfer fails with exit 1 and `Protocol "smb" is disabled`, and `curl -V` does not list them, as curl 8.22.0 does (ADR-0835, ADR-1263).' The code serves them: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))', and its doc at :48 says 'smb and smbs served by the SMB client (ADR-1281)'; Curl.Engine.UnitLibrary/ManagedCurlVersion.cs:18 lists "smb", "smbs" in -V's Protocols line. Changed by commit e019d9e5a8 'feat(smb): serve smb and smbs through the Smb client and list them in -V (BL-5286)'. An agent would tell users smb:// fails with exit 1. Phase 2: explained by ADR-1281 (smb and smbs are served as in the OpenSSL reference build, supersedes ADR-0835); ADR-1263, which README cites, still reads 'stay disabled in every build' and is not marked superseded.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path README.md -Pattern 'known but disabled in every build').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count; (Select-String -Path Curl.Engine.UnitLibrary/ManagedCurlVersion.cs -Pattern '"smb", "smbs"').Count
```

- Expected: 0, 1, 1: README describes smb/smbs as served and listed by -V, as the code does.
- Actual: 1, 1, 1: README says disabled and unlisted while the composition registers SmbProtocolHandler and -V lists smb and smbs.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
