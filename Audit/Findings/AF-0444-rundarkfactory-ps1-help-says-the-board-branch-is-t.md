---
id: AF-0444
title: RunDarkFactory.ps1 help says the board branch is the only one it force-pushes, but the shift-end merge force-pushes factory/merge-<stamp>
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
key: truthfulness:RunDarkFactory.ps1:board-branch-only-force-push:false-help
reproduction: none
task: BL-3521
tasks: BL-3521
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_1542.md, 2026-10-25_1944.md
---
# AF-0444 - RunDarkFactory.ps1 help says the board branch is the only one it force-pushes, but the shift-end merge force-pushes factory/merge-<stamp>

## Summary

High finding from the truthfulness auditor at `RunDarkFactory.ps1:369`: RunDarkFactory.ps1 help says the board branch is the only one it force-pushes, but the shift-end merge force-pushes factory/merge-<stamp>. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `RunDarkFactory.ps1:369`

RunDarkFactory.ps1:369-370 (comment-based help, heartbeat section): "The board branch is the only one this script force-pushes (Stewart, 2026-09-28)". The loader dot-sources DarkFactory/ShiftEnd.ps1 (RunDarkFactory.ps1:643, area list includes 'ShiftEnd'), and its Invoke-MergeToMaster, called from DarkFactory/ShiftCoordinator.ps1:515 at every shift-end merge, runs at DarkFactory/ShiftEnd.ps1:84: Invoke-Git @('push', '-q', '-f', 'origin', "${head}:refs/heads/$pin") with $pin = "factory/merge-$Stamp". So the script also force-pushes a factory/merge-* branch, and an agent checking the script's force pushes against CLAUDE.md's force-push rules would trust a false scope.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/ShiftEnd.ps1 -Pattern 'The board branch is the only one this script force-pushes', "'push', '-q', '-f', 'origin'" | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Either the help names every branch the script force-pushes (board and factory/merge-<stamp>), or ShiftEnd.ps1 pushes the pin without -f: the two patterns do not both match.
- Actual: RunDarkFactory.ps1:369
ShiftEnd.ps1:84

## Re-audits

- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the reproduction: 'The board branch is the only one this script force-pushes' no longer matches in RunDarkFactory.ps1; only DarkFactory/ShiftEnd.ps1:84 (the factory/merge-<stamp> force push) matches. The help at RunDarkFactory.ps1:369-370 now says 'This script force-pushes two branches: the board branch (Stewart, 2026-09-28) and, at the shift-end merge, the pin branch factory/merge-<stamp>', which matches the code.
- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Ran the reproduction at 390c05e0: the only match is ShiftEnd.ps1:84 (the force-push of factory/merge-$Stamp); 'The board branch is the only one this script force-pushes' no longer matches in RunDarkFactory.ps1. Its help (lines 369-370) now reads 'This script force-pushes two branches: the board branch (Stewart, 2026-09-28) and, at the shift-end merge, the pin branch factory/merge-<stamp>', which matches the code.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
