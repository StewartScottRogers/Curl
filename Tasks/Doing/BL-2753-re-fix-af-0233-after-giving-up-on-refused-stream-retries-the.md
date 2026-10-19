---
id: BL-2753
title: Re-fix AF-0233: After giving up on refused-stream retries the connection can be kept alive with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2753 — Re-fix AF-0233: After giving up on refused-stream retries the connection can be kept alive with no test failing

## Goal

The defect the audit office reported as AF-0233 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0233 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0233-after-giving-up-on-refused-stream-retries-the-conn.md`.

Re-fix: the earlier task(s) BL-2570 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: site resolved to line 1993 ('KeepsAlive: false' -> 'KeepsAlive: true' in StreamRefusedOutcome), outcome survived, excludedTests empty. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`

Mutant 'new HttpAttemptOutcome(refused with { ErrorMessage = ConnectionDiedGivingUp(...) }, null, KeepsAlive: false)' -> 'KeepsAlive: true' survived. A connection the peer kept refusing would go back to the pool, and the next URL of the same run could reuse it. No test checks the pool state after the 'Connection died, tried N times before giving up' outcome.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975:false -Member StreamRefusedOutcome -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
