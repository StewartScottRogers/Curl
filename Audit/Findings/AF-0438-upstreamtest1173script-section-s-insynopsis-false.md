---
id: AF-0438
title: UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:Section-false:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215:false
task: none
tasks:
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-25: filed proposed.
