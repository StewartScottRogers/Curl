---
id: AF-0202
title: ADR-0474 says the gap office excludes test1211 with an ADR-specific reason; the converter records the generic disabled-upstream reason
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0474-upstream-test1211-is-measured-excluded-because-upstream-disables-it-and-real-curl-exits-10.md:test1211:stale-adr
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0202 - ADR-0474 says the gap office excludes test1211 with an ADR-specific reason; the converter records the generic disabled-upstream reason

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0474-upstream-test1211-is-measured-excluded-because-upstream-disables-it-and-real-curl-exits-10.md:43`: ADR-0474 says the gap office excludes test1211 with an ADR-specific reason; the converter records the generic disabled-upstream reason.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0474-upstream-test1211-is-measured-excluded-because-upstream-disables-it-and-real-curl-exits-10.md:43`

Decision 3 states as fact: 'The gap office measures `behaviour:test1211` as `excluded`, with the reason "disabled upstream (tests/data/DISABLED); real curl 8.21.0 exits 10 after QUIT, which Curl matches (ADR-0474)"'. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no rule naming test1211 or that reason. Its rule 0 (line 132, 'if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }') excludes every DISABLED case with the generic reason 'disabled-upstream' and evidence 'disabled upstream' (self-test line 235). The status 'excluded' holds; the quoted reason does not. The rest of the ADR holds: FtpProtocolHandlerActiveModeTests.ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting exists, and test1211 is absent from PassingUpstreamCases.txt.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern 'test1211|exits 10').Count; (Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern "return @\('excluded', 'disabled-upstream'\)").Line.Trim()
```

- Expected: 1 or more (a rule that records the ADR's reason for test1211)
- Actual: 0
if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
