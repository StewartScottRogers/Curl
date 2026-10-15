---
id: AF-0159
title: ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:ADR-0473:stale-adr
reproduction: none
task: BL-2052
tasks: BL-2052
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0159 - ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:40`: ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured. Reported by an auditor flagged unreliable in 2026-10-11_0051.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:40`

Decision 2 (line 40) says, as fact: 'The gap office measures `behaviour:test2043` as `excluded`, with the reason "reaches revoked.badssl.com on the internet; ..."'. Decision 3 says the same for every case screened out for reaching an internet host. Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs:228-234 skips such cases with the reason 'the case reaches <host> on the internet, which the harness does not'. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 Get-SkippedVerdict (lines 131-149) gives `excluded` only for disabled-upstream, libcurl-api, libcurl-unit-test, debug-build-only and reference-lacks. A skipped case with no <server> falls through to `return @('unmeasured', 'harness-unsupported')`, and the converter never mentions 'internet' or test2043. The ADR's Consequences section admits the exclusion 'is left to an interactive session's next gap run', but the Decision states it as done when it is only intended.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern 'internet|test2043').Count; (Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern "return @\('excluded'").Count
```

- Expected: At least 1 match for an internet-host exclusion rule, so test2043 is measured excluded as the ADR states
- Actual: 0, then 5: no internet-host rule; the five excluded rules are disabled-upstream, libcurl-api, libcurl-unit-test, debug-build-only and reference-lacks, so test2043 falls to unmeasured, harness-unsupported

## Re-audits

- 2026-10-11 | 2026-10-11_1638.md | not re-audited | Ran the reproduction: Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 still has 0 lines matching 'internet|test2043' and 5 "return @('excluded'" rules, so the converter still has no internet-host rule. The finding's defect was ADR-0473 stating as fact that the gap office records test2043 as excluded, and that statement has since been rewritten as intent: Decision 2 now reads 'This is intent: today Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no internet-host rule, so it records the skipped case as unmeasured (harness-unsupported)'. The reproduction checks only the converter, not the ADR's wording, so it cannot tell whether the false statement remains: not re-audited.
- 2026-10-12 | 2026-10-12_1653.md | not re-audited | Ran the reproduction. It printed 0 then 5, the same as before, because it checks only the gap converter, which still has no internet-host or test2043 rule. But the defect was in ADR-0473's wording, and that has changed. Decision 2 now reads: 'This is intent: today Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no internet-host rule, so it records the skipped case as unmeasured (harness-unsupported)', which is true of the converter. The reproduction cannot show whether the ADR now writes intent as intent, so it cannot tell whether the finding is fixed. Recorded as not re-audited.
- 2026-10-13 | 2026-10-13_0925.md | not re-audited | Ran the reproduction: 'internet|test2043' count = 0 and "return @('excluded'" count = 5, the same counts as when filed. But the reproduction measures only the converter, and the defect was the ADR's statement. ADR-0473 decision 2 (lines 40-43) now reads 'The gap office is to measure behaviour:test2043 as excluded ... This is intent: today Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no internet-host rule, so it records the skipped case as unmeasured (harness-unsupported).' That is true of the code. The reproduction's result cannot tell the document fix apart, so this is not re-audited. The false statement it was filed for no longer appears in the ADR.
- 2026-10-13 | 2026-10-13_1822.md | not re-audited | Ran the reproduction: 0 (no 'internet|test2043' rule in Gap/Tools/ConvertTo-BehaviourMeasurement.ps1) and 5 'excluded' returns, so the converter is unchanged. But the finding's defect was the ADR's claim, and ADR-0473 decision 2 (lines 40-43) now says the excluded measurement is intent: 'This is intent: today Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no internet-host rule, so it records the skipped case as unmeasured (harness-unsupported).' The reproduction only checks the converter, not the ADR text, so it cannot tell whether the false statement remains; read by hand, the ADR is now true of the code.
- 2026-10-14 | 2026-10-14_0453.md | not re-audited | Ran the reproduction: 0 matches for 'internet|test2043' and 5 for "return @\('excluded'" in Gap/Tools/ConvertTo-BehaviourMeasurement.ps1. The converter is unchanged (still no internet-host rule), so the reproduction measures only the code side and cannot tell. ADR-0473's Decision 2 now says 'This is intent: today ... has no internet-host rule, so it records the skipped case as unmeasured', so the ADR now matches the code. Recorded as not re-audited.
- 2026-10-14 | 2026-10-14_1815.md | not re-audited | Ran the reproduction: 'internet|test2043' matches 0 lines in Gap/Tools/ConvertTo-BehaviourMeasurement.ps1, and "return @('excluded'" matches 8. The converter output is unchanged. The fix went on the ADR side instead: ADR-0473 decision 2 now reads 'The gap office is to measure behaviour:test2043 as excluded ... This is intent: today Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no internet-host rule, so it records the skipped case as unmeasured (harness-unsupported)'. That matches the code. The reproduction only searches the converter, so it cannot tell whether the mismatch remains; read by hand, it is gone except for the title's present-tense 'is measured excluded'.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
