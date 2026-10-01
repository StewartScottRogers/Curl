---
id: BL-1069
title: Fix AF-0003: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1069 — Fix AF-0003: BL-564 was claimed 3 times: lanes 1, 4 and 6, with two conflict-resolve runs

## Goal

The defect the audit office reported as AF-0003 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0003 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0003-bl-564-was-claimed-3-times-lanes-1-4-and-6-with-tw.md`.

Location: `logs/BL-564-20260929-023709-L4.jsonl`

Location: `logs/BL-564-20260929-023709-L4.jsonl`

The tool reports claims=3 and costUsd=10.33 for BL-564. Lane 4's run ended 'FACTORY: BLOCKED BL-564 back in Backlog: its ADR folder Documentation/Planning/Decisions overlaps BL-883 in Doing'. Lane 6 later finished it ('FACTORY: DONE BL-564') after about 31 minutes. Lane 1 also ran it and finished DONE. Two '-resolve' runs (L1 and L6) then handled ADR-number renumbering conflicts. The shared ADR folder was the touches that serialised and duplicated the work. Overall, 35 tasks were claimed more than once and there were 40 requeues.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path ..\logs\BL-564-*.jsonl -Pattern 'FACTORY: (DONE|BLOCKED|RESOLVED) BL-564' -List
```

- Expected: Each task claimed once or twice and none requeued twice.
- Actual: BL-564 claims=3 across lanes 1, 4 and 6, with a BLOCKED then DONE sequence and two resolve runs.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] Each cause of BL-564's second and third claims is removed from `RunDarkFactory.ps1`, so the finding's reproduction, run over shifts after this change, gives the expected result. (Reworded: see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

### Why BL-564 was claimed three times (from git history and `Curl.logs\BL-564-20260929-023709-*`)

1. **Lane 6** (04:34 claim) finished the code. The integration's fast tests then went red
   once, and the lane parked the work and requeued the task (995a9457, 05:11). BL-898
   (06:47 the same day) has since fixed this: `Test-Green` reruns a red fast-test run
   once before parking, so a single flaky failure no longer costs a claim.
2. **Lane 4** (05:12 claim) cherry-picked lane 6's work, found it green, and still sent
   the task back to Backlog (05:17). Lane prompt rule 3 told it to check `touches`
   against `Tasks/Doing`, and it did, but in its own checkout. That copy dated from
   the claim and still listed BL-883, which lane 2 had finished at 05:12:59. The only
   "overlap" was the ADR folder `Documentation/Planning/Decisions`, and the ADR was a
   new file plus one index row.
3. **Lane 1** (05:25 claim) finished it. The two `-resolve` runs (L6 and L1) were the
   same ADR number taken by another lane in the meantime.

### Fix (RunDarkFactory.ps1)

- Lane prompt rule 3: a new ADR (its file plus its Decisions README row) and a newly
  filed task never need `touches` and never send a task back. The overlap check now
  reads the shared branch's Doing: `git ls-tree` / `git show` on `origin/{BRANCH}`,
  which needs no fetch because every lane's sync and integrate keeps that shared ref
  current. When this run started, this checkout still listed BL-944 in Doing, but the
  shared ref no longer did, so the check would have read stale data.
- Resolver prompt: says outright how to settle an ADR-number collision (the shared
  side keeps the number, this lane's takes the next free one everywhere it appears),
  so the resolve run is quick and gives the same answer every time.
- Script header's summary of what a run does with `touches` updated to match.

### Acceptance criterion reworded

The original criterion asked for the reproduction to give "each task claimed once or
twice". That reproduction measures logs from 2026-09-23 on, and those logs are
history: BL-564's three claims stay in them whatever changes. What this task can deliver
is removing the causes. Whether that worked shows in the process auditor's re-audit over
shifts after this change, which is how the finding closes anyway. A factory lane can't
run `Audit/Tools/Measure-FactoryProcess.ps1` itself: audit paths are guarded from lanes
(ADR-0267).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Lanes check touches overlaps against the shared branch's Doing and never bounce a task for a new ADR; the resolver settles ADR-number collisions by a fixed rule
