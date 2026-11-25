---
id: AF-0988
title: Surviving mutant: the header store can count every later line as a new request once a final head ended (finalHeadEnded = false -> true)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferHeaderStore.cs:BeginNextRequestAfterFinalHead-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235:false
task: BL-5790
tasks: BL-5790
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed: 2026-11-25
closed-how: mechanical
closed-by: 2026-11-25_1306.md
---
# AF-0988 - Surviving mutant: the header store can count every later line as a new request once a final head ended (finalHeadEnded = false -> true)

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235`: Surviving mutant: the header store can count every later line as a new request once a final head ended (finalHeadEnded = false -> true).

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235`

Seeded mutation run: in BeginNextRequestAfterFinalHead, 'finalHeadEnded = false;' -> 'finalHeadEnded = true;' survived. With the mutant the flag never resets, so request++ runs on every later call and curl_easy_header request indexes after the first redirect (or other repeated request) are numbered wrong. The tests cover two responses, where the bug does not show yet.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235:false -Member BeginNextRequestAfterFinalHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: no | Ran the -Site command: line 235, 'finalHeadEnded = false' -> 'true', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-25: accepted -> closed. Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
