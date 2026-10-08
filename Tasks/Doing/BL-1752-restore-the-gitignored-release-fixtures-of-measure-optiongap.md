---
id: BL-1752
title: Restore the gitignored release fixtures of Measure-OptionGap.ps1 and Measure-WriteOutGap.ps1 self-tests
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools/Fixtures/options, Gap/Tools/Fixtures/writeout, Gap/Tools/Measure-OptionGap.ps1, Gap/Tools/Measure-WriteOutGap.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1752 — Restore the gitignored release fixtures of Measure-OptionGap.ps1 and Measure-WriteOutGap.ps1 self-tests

## Goal

`Gap/Tools/Measure-OptionGap.ps1 -SelfTest` and `Gap/Tools/Measure-WriteOutGap.ps1 -SelfTest`
pass in a fresh clone, because their fake releases are committed.

## Context

Both self-tests read a fake release from `Gap/Tools/Fixtures/options/release` and
`Gap/Tools/Fixtures/writeout/release`, but `.gitignore` (`[Rr]elease/`) ignores those
folders, so BL-1723 and BL-1725 committed only the probe-result files. In a clean checkout
`Measure-OptionGap.ps1 -SelfTest` throws PathNotFound from Get-ChildItem. Found by BL-1735
on 2026-10-08. Recreate each fake release from what its self-test asserts (options:
--fail, --max-time, --verbose with the asserted front matter; writeout: a write-out.md the
self-test's checks match) and add it with `git add -f`, as `Gap/Tools/Fixtures/release`
was, or rename the folder (for example `upstream`) and update the script. `.gitignore` is
not in `touches`.

## Acceptance criteria

- [ ] `git ls-files Gap/Tools/Fixtures/options Gap/Tools/Fixtures/writeout` lists the fake release files.
- [ ] Both `-SelfTest` runs print PASS lines and no FAIL under Windows PowerShell 5.1 and PowerShell 7 in a clean checkout.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
