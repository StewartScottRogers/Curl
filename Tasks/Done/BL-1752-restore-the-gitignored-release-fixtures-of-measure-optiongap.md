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
completed: 2026-10-08
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

- [x] `git ls-files Gap/Tools/Fixtures/options Gap/Tools/Fixtures/writeout` lists the fake release files.
- [x] Both `-SelfTest` runs print PASS lines and no FAIL under Windows PowerShell 5.1 and PowerShell 7 in a clean checkout.

## Notes

- Renamed the fake releases from `release` to `upstream` (as `Fixtures/environment/upstream` and `Fixtures/version/upstream` already are) rather than `git add -f`: `[Rr]elease/` can never hide them again and no one has to remember the force. Both scripts' self-tests now read `Fixtures/<area>/upstream`.
- options: `fail.md` (no Short, Protocols HTTP, boolean), `max-time.md` (Short m, Arg <seconds>, single), `verbose.md` (Short v, boolean, Added 4.0), plus `_OPTIONS.md` and `MANPAGE.md` carrying a Long field so the skip check is real.
- writeout: `write-out.md` with a heading before "The variables available are:" and one after the closing `##`, so the "nothing outside the list" check is real; `header{name}` (Added in 7.84.0), `time_total` (Added in 7.9.7), `content_type` with no Added.
- Verified from a copy made with `git checkout-index` of only tracked files: 10 PASS, 0 FAIL for each script under Windows PowerShell 5.1 and PowerShell 7. dotnet build clean, fast tests green.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-OptionGap.ps1 and Measure-WriteOutGap.ps1 -SelfTest pass in a clean checkout from committed Fixtures/<area>/upstream releases
