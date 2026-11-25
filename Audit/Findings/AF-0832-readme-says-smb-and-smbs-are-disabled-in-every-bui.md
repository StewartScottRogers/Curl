---
id: AF-0832
title: README says smb and smbs are disabled in every build and absent from curl -V, but Curl serves and lists them
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-11-18_0810.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-17_0430.md, 2026-11-18_0810.md).
key: truthfulness:README.md:smb-smbs-disabled:false-statement
reproduction: none
task: BL-5306
tasks: BL-5306
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed: 2026-11-18
closed-how: consecutive
closed-by: 2026-11-17_0430.md, 2026-11-18_0810.md
---
# AF-0832 - README says smb and smbs are disabled in every build and absent from curl -V, but Curl serves and lists them

## Summary

High finding from the truthfulness auditor at `README.md:23`: README says smb and smbs are disabled in every build and absent from curl -V, but Curl serves and lists them.

## Evidence

Location: `README.md:23`

README.md:23-26: '`smb` and `smbs` are known but disabled in every build: a transfer fails with exit 1 and `Protocol "smb" is disabled`, and `curl -V` does not list them, as curl 8.22.0 does (ADR-0835, ADR-1263).' The code serves them: Curl.Engine.UnitLibrary/EngineComposition.cs:197 'new LazyProtocolHandler(["smb", "smbs"], () => new SmbProtocolHandler(recordingConnector))', and its doc at :48 says 'smb and smbs served by the SMB client (ADR-1281)'; Curl.Engine.UnitLibrary/ManagedCurlVersion.cs:18 lists "smb", "smbs" in -V's Protocols line. Changed by commit e019d9e5a8 'feat(smb): serve smb and smbs through the Smb client and list them in -V (BL-5286)'. An agent would tell users smb:// fails with exit 1. Phase 2: explained by ADR-1281 (smb and smbs are served as in the OpenSSL reference build, supersedes ADR-0835); ADR-1263, which README cites, still reads 'stay disabled in every build' and is not marked superseded.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path README.md -Pattern 'known but disabled in every build').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count; (Select-String -Path Curl.Engine.UnitLibrary/ManagedCurlVersion.cs -Pattern '"smb", "smbs"').Count
```

- Expected: 0, 1, 1: README describes smb/smbs as served and listed by -V, as the code does.
- Actual: 1, 1, 1: README says disabled and unlisted while the composition registers SmbProtocolHandler and -V lists smb and smbs.

## Re-audits

- 2026-11-17 | 2026-11-17_0430.md | reproduces: no | Ran the reproduction: README.md 'known but disabled in every build' count 0; EngineComposition.cs 'new SmbProtocolHandler' count 1 (line 197); ManagedCurlVersion.cs '"smb", "smbs"' count 1 (line 18). README.md:23-26 now lists smb and smbs among the served Original schemes and says curl -V lists them (ADR-1281), which matches the code.
- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the reproduction: README.md 'known but disabled in every build' count 0. EngineComposition 'new SmbProtocolHandler' count 1 and ManagedCurlVersion '"smb", "smbs"' count 1. README.md:24 now says smb and smbs are served by the SMB client and listed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-17_0430.md, 2026-11-18_0810.md).
