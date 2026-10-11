---
id: AF-0159
title: ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:ADR-0473:stale-adr
reproduction: none
task: none
tasks:
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

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
