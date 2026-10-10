---
id: AF-0132
title: h2c upgrade's 'afterHead.Length > 0' can become '>= 0' with no test failing: a spurious 'Copied HTTP/2 data ... len=0' -v line
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-09_1435.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:SwitchAsync-gt:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179:>
task: BL-1884
tasks: BL-1884
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed: 2026-10-09
closed-how: mechanical
closed-by: 2026-10-09_1435.md
---
# AF-0132 - h2c upgrade's 'afterHead.Length > 0' can become '>= 0' with no test failing: a spurious 'Copied HTTP/2 data ... len=0' -v line

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179`: h2c upgrade's 'afterHead.Length > 0' can become '>= 0' with no test failing: a spurious 'Copied HTTP/2 data ... len=0' -v line. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived  Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179 >' (member SwitchAsync, 'if (afterHead.Length > 0)' -> 'if (afterHead.Length >= 0)'), and the targeted -Site run confirmed it survived. The guard decides whether -v writes curl's 'Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=N' line after the 101 head. With the mutant, an upgrade where no HTTP/2 bytes arrived in the same read as the 101 writes 'len=0', a line curl never prints, so verbose stderr bytes differ. The only tests touching the line (HttpProtocolHandlerTests.H2cUpgrade.cs:107, HttpProtocolHandlerTests.Http2Trace.cs:105) assert it when data followed (len=31, len=42). No test asserts that it is absent when nothing followed the head.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179:> -Member SwitchAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the -Site command with -ExcludeBaselineFailures: HttpH2cUpgradeConnection.cs:179 'if (afterHead.Length > 0)' -> '>= 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
