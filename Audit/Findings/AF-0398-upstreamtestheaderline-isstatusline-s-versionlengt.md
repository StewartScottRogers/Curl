---
id: AF-0398
title: UpstreamTestHeaderLine.IsStatusLine's versionLength + 1 bound can become - 1 with no test failing, so a short HTTP/ line throws instead of being judged
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:IsStatusLine-plus1:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33:+1
task: BL-3445
tasks: BL-3445
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed: 2026-10-24
closed-how: mechanical
closed-by: 2026-10-24_0845.md
---
# AF-0398 - UpstreamTestHeaderLine.IsStatusLine's versionLength + 1 bound can become - 1 with no test failing, so a short HTTP/ line throws instead of being judged

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33`: UpstreamTestHeaderLine.IsStatusLine's versionLength + 1 bound can become - 1 with no test failing, so a short HTTP/ line throws instead of being judged. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33`

Sampled mutant (seed 0) survived: '&& rest.Length > versionLength + 1' -> '&& rest.Length > versionLength - 1'. The bound guards the next two reads, rest[versionLength] and rest[versionLength + 1]; with the mutant a line such as 'HTTP/1.1' or 'HTTP/1.1 ' reaches them and throws IndexOutOfRangeException, so the harness's header comparison of an upstream case crashes instead of returning a verdict. No test gives IsStatusLine a status line cut short after the version.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33:+1 -Member IsStatusLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Ran the -Site Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33:+1 run: resolvedLine 33, 'rest.Length > versionLength + 1' -> '- 1', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
