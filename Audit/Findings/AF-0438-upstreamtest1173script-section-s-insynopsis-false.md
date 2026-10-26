---
id: AF-0438
title: UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-25_1542.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:Section-false:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215:false
task: BL-3516
tasks: BL-3516
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed: 2026-10-25
closed-how: mechanical
closed-by: 2026-10-25_1542.md
---
# AF-0438 - UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215`: UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215`

Sampled mutant (seed 0) survived: in the ExampleHeading branch, 'state.InSynopsis = false;' -> 'state.InSynopsis = true;'. The man-page checker then treats an EXAMPLE section as SYNOPSIS, so test1173's synopsis checks run on example text. That changes the problems the script reports and its exit code. No Curl.Conformance.UnitTests test gives it a page whose EXAMPLE section follows SYNOPSIS and would break a synopsis rule.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215:false -Member Section -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the -Site Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215:false -Member Section command: resolvedLine 215, outcome killed (excludedTests: UpstreamCase_RunThroughCurl_HoldsTheRatchet, which fails unmutated). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1542.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
