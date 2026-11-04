---
id: AF-0656
title: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:ReportReplyHead-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165:<
task: BL-4838
tasks: BL-4838
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_1442.md
---
# AF-0656 - Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`

Mutant 'lineFeed < 0' -> 'lineFeed <= 0' survived. When the rest of the head starts with an LF, the mutant writes all of it as one line instead of splitting it, which changes the verbose output bytes. No test covers an LF at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165:< -Member ReportReplyHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the -Site command: resolvedLine 165, outcome killed (baseline left out the 3 HandBuiltTlsProviderTests ECH tests that fail unmutated, filed separately). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
