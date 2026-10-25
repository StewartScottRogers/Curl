---
id: BL-3521
title: Fix AF-0444: RunDarkFactory.ps1 help says the board branch is the only one it force-pushes, but the shift-end merge force-pushes factory/merge-<stamp>
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3521 — Fix AF-0444: RunDarkFactory.ps1 help says the board branch is the only one it force-pushes, but the shift-end merge force-pushes factory/merge-<stamp>

## Goal

The defect the audit office reported as AF-0444 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0444 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0444-rundarkfactory-ps1-help-says-the-board-branch-is-t.md`.

Location: `RunDarkFactory.ps1:369`

Location: `RunDarkFactory.ps1:369`

RunDarkFactory.ps1:369-370 (comment-based help, heartbeat section): "The board branch is the only one this script force-pushes (Stewart, 2026-09-28)". The loader dot-sources DarkFactory/ShiftEnd.ps1 (RunDarkFactory.ps1:643, area list includes 'ShiftEnd'), and its Invoke-MergeToMaster, called from DarkFactory/ShiftCoordinator.ps1:515 at every shift-end merge, runs at DarkFactory/ShiftEnd.ps1:84: Invoke-Git @('push', '-q', '-f', 'origin', "${head}:refs/heads/$pin") with $pin = "factory/merge-$Stamp". So the script also force-pushes a factory/merge-* branch, and an agent checking the script's force pushes against CLAUDE.md's force-push rules would trust a false scope.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftEnd.ps1 -Pattern 'The board branch is the only one this script force-pushes', "'push', '-q', '-f', 'origin'" | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Either the help names every branch the script force-pushes (board and factory/merge-<stamp>), or ShiftEnd.ps1 pushes the pin without -f: the two patterns do not both match.
- Actual: RunDarkFactory.ps1:369
ShiftEnd.ps1:84

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
