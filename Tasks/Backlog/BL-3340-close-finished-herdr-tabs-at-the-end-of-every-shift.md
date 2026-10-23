---
id: BL-3340
title: Close finished herdr tabs at the end of every shift
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/HerdrTabs.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3340 — Close finished herdr tabs at the end of every shift

## Goal

When a shift ends (cleanly, with work waiting on Stewart, or by handing over to the next
`-Continuous` shift), its coordinator closes every finished factory, audit and gap tab in
its herdr workspace, so Stewart never has to ask for "close old herdr tabs".

## Context

Stewart, 2026-10-23: "Can you automaticly do this at the end of the shift? 'close old
herdr tabs'". Until now an interactive session swept the tabs by hand whenever he asked
(the memory note "close old herdr tabs"): run `herdr tab list` and close every tab except
tab "1", the Claude session's tab, and any tab whose run is still alive. The factory
already closes a lane's tab when the lane ends cleanly (`Close-HerdrTab` in
`DarkFactory/HerdrTabs.ps1`, called from `DarkFactory/ShiftCoordinator.ps1`). It leaves
open lanes captioned `BLOCKED, read`, `STALLED, read` and `no report, read`, tabs left by
shifts that died or by a reboot, and finished `Audit HH:mm ...` and `Gap HH:mm` tabs.

What herdr gives you (0.9.4, measured 2026-10-23):
- `herdr tab list` returns JSON `result.tabs[]` with `tab_id`, `label` and `workspace_id`.
- `herdr pane list` returns `result.panes[]` with `pane_id` and `tab_id`.
- `herdr pane process-info --pane <id>` returns `result.process_info.shell_pid`. The
  foreground process is always the pane's own `pwsh.exe`, so it cannot tell you whether a
  run is alive. A live run is a descendant of `shell_pid` (walk `Win32_Process`
  `ParentProcessId`) whose command line names `RunDarkFactory.ps1`, `RunAudit.ps1` or
  `RunGapAnalysis.ps1`.

## Acceptance criteria

- [ ] `DarkFactory/HerdrTabs.ps1` has a function, named for what it does (for example
      `Close-FinishedHerdrTabs`). It closes every tab in `$env:HERDR_WORKSPACE_ID` whose
      label is a factory, audit or gap caption (`DF *`, `Dark factory*`, `Audit *`,
      `Gap *`) and whose pane has no live run under its shell as defined above. Captions
      that end in "read" are included.
- [ ] It never closes this coordinator's own tab, a tab whose label is not one of those
      captions (so tab "1" and the "Claude - Curl" session tab survive), or a tab with a
      live run under it (the next shift's tab included).
- [ ] It does nothing outside herdr (`Get-HerdrBin` is `$null`), and when a herdr call
      fails or returns JSON it cannot parse, it writes one trace line and lets the shift end
      normally.
- [ ] `DarkFactory/ShiftCoordinator.ps1` calls it on every way a shift ends: before
      `Close-OwnHerdrTab` on a handover, at shift complete, and when the shift ends with
      work waiting on Stewart. The trace says how many tabs it closed, and the shift-end
      summary includes `closed N finished tabs`.
- [ ] The comment that describes captions in `HerdrTabs.ps1` and the script help in
      `RunDarkFactory.ps1`, if it describes tab closing, say that finished tabs are swept at
      shift end. Changing the help is allowed only if that block already lives in a file
      this task touches. Otherwise file a follow-up task.
- [ ] Checked by hand inside herdr: one dead `DF` tab and one live one. The dead tab
      closes, the live one and tab "1" stay. Record the result in the Log.

## Notes

Lanes that end `BLOCKED, read` still raise the shift-end alarm, and their trace and
summary stay in `<repo>.logs\`, so closing their tabs loses nothing. That is why
Stewart's standing rule closes them too.

## Log

- 2026-10-23: Created.
