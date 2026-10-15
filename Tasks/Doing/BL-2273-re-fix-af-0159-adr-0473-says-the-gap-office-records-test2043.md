---
id: BL-2273
title: Re-fix AF-0159: ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-15
completed:
---
# BL-2273 — Re-fix AF-0159: ADR-0473 says the gap office records test2043 as excluded with an internet-host reason; the gap converter has no such rule and records it as unmeasured

## Goal

The defect the audit office reported as AF-0159 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0159 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0159-adr-0473-says-the-gap-office-records-test2043-as-e.md`.

Re-fix: the earlier task(s) BL-2052 reached Done, and a later re-audit by the truthfulness auditor found the reproduction still reproduces:

- 2026-10-15 | 2026-10-15_0156.md | reproduces: yes | Ran the reproduction: (Select-String ... -Pattern 'internet|test2043').Count = 0, and (Select-String ... -Pattern "return @\('excluded'").Count = 8. The converter still has no internet-host or test2043 rule, so test2043 is not excluded as ADR-0473 says.

Location: `Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:40`

Location: `Documentation/Planning/Decisions/ADR-0473-upstream-test2043-is-measured-excluded-because-it-checks-a-live-internet-host.md:40`

Decision 2 (line 40) says, as fact: 'The gap office measures `behaviour:test2043` as `excluded`, with the reason "reaches revoked.badssl.com on the internet; ..."'. Decision 3 says the same for every case screened out for reaching an internet host. Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs:228-234 skips such cases with the reason 'the case reaches <host> on the internet, which the harness does not'. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 Get-SkippedVerdict (lines 131-149) gives `excluded` only for disabled-upstream, libcurl-api, libcurl-unit-test, debug-build-only and reference-lacks. A skipped case with no <server> falls through to `return @('unmeasured', 'harness-unsupported')`, and the converter never mentions 'internet' or test2043. The ADR's Consequences section admits the exclusion 'is left to an interactive session's next gap run', but the Decision states it as done when it is only intended.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern 'internet|test2043').Count; (Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern "return @\('excluded'").Count
```

- Expected: At least 1 match for an internet-host exclusion rule, so test2043 is measured excluded as the ADR states
- Actual: 0, then 5: no internet-host rule; the five excluded rules are disabled-upstream, libcurl-api, libcurl-unit-test, debug-build-only and reference-lacks, so test2043 falls to unmeasured, harness-unsupported

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
