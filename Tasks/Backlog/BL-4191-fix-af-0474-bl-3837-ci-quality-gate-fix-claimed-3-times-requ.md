---
id: BL-4191
title: Fix AF-0474: BL-3837 (CI quality-gate fix) claimed 3 times: requeued on BL-3831, then Blocked on an unrelated Linux failure
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4191 — Fix AF-0474: BL-3837 (CI quality-gate fix) claimed 3 times: requeued on BL-3831, then Blocked on an unrelated Linux failure

## Goal

The cause of the factory behaviour the audit office reported as AF-0474 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0474 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0474-bl-3837-ci-quality-gate-fix-claimed-3-times-requeu.md`.

Location: `logs/DarkFactory-20261026-063535-L2.log:107`

Location: `logs/DarkFactory-20261026-063535-L2.log:107`

The task Log shows 3 claims on 2026-10-26; the tool counts 2 (claims=2, $0.85). Requeue at 08:59:32: 'Waits on BL-3831, which covers WriteRefusalAsync's untaken branch' (DarkFactory-20261026-063535-L2.log:107). Then Doing -> Blocked because Build and test (ubuntu-latest) still failed on Format_OpenSslUnprintableNames (BL-3878's test), and Blocked -> Backlog once BL-3878 was Done. Claim 3 (DarkFactory-20261026-151632-L2.log:47, 16:37:42) finished it. A CI-fix task was filed while the task that fixed it (BL-3831) was already on the board.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3837 (claim|REQUEUE|BLOCKED|DONE)'; Select-String -Path Tasks/Done/2026-10-26/BL-3837-*.md -Pattern 'Backlog -> Doing'
```

- Expected: Fewer than 3 claims.
- Actual: 3 'Backlog -> Doing' lines in the task Log; claims at 08:54:54 (L2), and twice more in shift 20261026-151632.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
