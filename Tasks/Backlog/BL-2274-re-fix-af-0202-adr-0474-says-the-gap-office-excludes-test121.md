---
id: BL-2274
title: Re-fix AF-0202: ADR-0474 says the gap office excludes test1211 with an ADR-specific reason; the converter records the generic disabled-upstream reason
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-15
completed:
---
# BL-2274 — Re-fix AF-0202: ADR-0474 says the gap office excludes test1211 with an ADR-specific reason; the converter records the generic disabled-upstream reason

## Goal

The defect the audit office reported as AF-0202 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0202 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0202-adr-0474-says-the-gap-office-excludes-test1211-wit.md`.

Re-fix: the earlier task(s) BL-2195 reached Done, and a later re-audit by the truthfulness auditor found the reproduction still reproduces:

- 2026-10-15 | 2026-10-15_0156.md | reproduces: yes | Ran the reproduction: (Select-String ... -Pattern 'test1211|exits 10').Count = 0. The disabled-upstream line is still `if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }`: the generic reason, not ADR-0474's specific one.

Location: `Documentation/Planning/Decisions/ADR-0474-upstream-test1211-is-measured-excluded-because-upstream-disables-it-and-real-curl-exits-10.md:43`

Location: `Documentation/Planning/Decisions/ADR-0474-upstream-test1211-is-measured-excluded-because-upstream-disables-it-and-real-curl-exits-10.md:43`

Decision 3 states as fact: 'The gap office measures `behaviour:test1211` as `excluded`, with the reason "disabled upstream (tests/data/DISABLED); real curl 8.21.0 exits 10 after QUIT, which Curl matches (ADR-0474)"'. Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 has no rule naming test1211 or that reason. Its rule 0 (line 132, 'if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }') excludes every DISABLED case with the generic reason 'disabled-upstream' and evidence 'disabled upstream' (self-test line 235). The status 'excluded' holds; the quoted reason does not. The rest of the ADR holds: FtpProtocolHandlerActiveModeTests.ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting exists, and test1211 is absent from PassingUpstreamCases.txt.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern 'test1211|exits 10').Count; (Select-String -Path Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -Pattern "return @\('excluded', 'disabled-upstream'\)").Line.Trim()
```

- Expected: 1 or more (a rule that records the ADR's reason for test1211)
- Actual: 0
if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-15: Created.
