---
id: AF-0008
title: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/ConnectToMappings.cs:Failure-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ConnectToMappings.cs:135:false
task: BL-1264
tasks: BL-1264
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0008 - ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Mutant survived (seed 0): `new(string.Empty, 0, IsMapped: false, message)` became `IsMapped: true`. A failed --connect-to parse could then claim a mapping, and no test checks IsMapped on a failure result.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectToMappings.cs:135:false -Member Failure -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 (false) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 false

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | Same red baseline, no mutants run. ConnectToMappings (ConnectDestination.cs) still creates IsMapped: false at lines 66 and 135. A fix is not shown.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | Networking run, seed 0: no mutant sampled in ConnectToMappings.cs (line 135 still has IsMapped: false); not shown surviving by the reproduction, unverified.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Seed-0 run did not sample the site. Hand-mutated ConnectToMappings.Failure to IsMapped: true in a scratch copy: Map_WhenTheMatchingDestinationDoesNotParse_ReportsCurlsExit49Message failed, so the mutant is killed.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | By hand, same method as AF-0006: ConnectToMappings.cs:135 Failure's 'IsMapped: false' -> 'IsMapped: true'. Killed: 9 failures, including Map_WhenTheMatchingDestinationDoesNotParse_ReportsCurlsExit49Message (three rows).
- 2026-10-07 | 2026-10-07_1336.md | not re-audited | overlaps planted defect PD-101 in Curl.Cryptography.UnitLibrary/AeadChaCha20Poly1305.cs, so the auditor's verdict (reproduces no) is set aside: Ran the -Site reproduction: resolvedLine 135, outcome killed.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
