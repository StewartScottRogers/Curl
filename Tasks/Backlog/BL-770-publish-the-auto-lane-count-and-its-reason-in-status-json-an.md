---
id: BL-770
title: Publish the auto lane count and its reason in status.json and show it on the task board page
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-768, BL-760, BL-761]
touches: [RunDarkFactory.ps1, .github/board]
requirement: none
created: 2026-09-28
completed:
---
# BL-770 — Publish the auto lane count and its reason in status.json and show it on the task board page

## Goal

During an Auto shift, the `status.json` the coordinator publishes to the `board` branch carries a top-level `autoLanes` object: the current lane count, the target, what binds it, and the last change's reason and time. The live task board page shows it as one line above the lane cards, so Stewart can see why the factory runs the lanes it does.

## Context

- This is Stewart's design approved 2026-09-28: every lane change is logged with its reason and feeds the board page. BL-763's ADR, item 10, fixes the object. BL-762's ADR fixes `status.json` schema 1, and its readers ignore unknown fields, so the schema stays 1.
- **The object:**

  ```json
  "autoLanes": { "lanes": 4, "target": 5.2, "binding": "weekly pace",
                 "reason": "lanes 3 -> 4 (weekly pace allows 5.2)", "changedAt": "2026-09-28T13:05:00Z" }
  ```

  - `target` is the step's `Desired` rounded to one decimal, or `null` while there is no burn rate yet.
  - `binding` is the limit's name: `5-hour pace`, `weekly pace`, `capacity`, `machine`, `maximum` or `no burn rate`.
  - `reason` is the last change's trace text, or the start line `lanes auto: starting at <n> (...)` until the first change.
  - `changedAt` is UTC `yyyy-MM-ddTHH:mm:ssZ`.
  - A fixed-lane shift publishes `"autoLanes": null`.
- **Where the code goes:**
  - `RunDarkFactory.ps1`: the auto-lanes step BL-768 added in the coordinator's wait loop, and the status merge and publish code BL-760 added. Keep the step's latest values in one script-scope variable that the publisher reads, and do not publish from the step. BL-760's `-TestHeartbeat` prints a merged `status.json`; give its fake status an `autoLanes` object.
  - `.github/board/site/index.html`, from BL-757 and BL-761: the lane cards render into `section#lanes`. Add one line above them: `Auto lanes: 4 - lanes 3 -> 4 (weekly pace allows 5.2), 13:05 UTC`. The line is absent when `autoLanes` is `null` or missing.
  - `.github/board/fixtures/status.json` (BL-761): add the object above.
- **Local preview:** `dotnet run --file .github/gource/serve.cs -- .github/board 8000`. Headless check: `"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu --virtual-time-budget=10000 --dump-dom "<url>"`, as in BL-761.
- **Do not start a shift to test.** Lanes are denied `git push`, so no real publish happens here. The next shift publishes it.
- **Style.** PowerShell only, 5.1 compatible, and no new non-ASCII characters in the script. The page stays plain inline HTML, CSS and JavaScript with no external resource.

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestHeartbeat` exits 0. The `status.json` it prints parses with `ConvertFrom-Json` and has `autoLanes` with exactly the fields `lanes`, `target`, `binding`, `reason` and `changedAt`, and `schema` still 1.
- [ ] Reading the diff shows each of these:
  - the Auto step updates the script-scope value on every step;
  - the publisher writes it as `autoLanes`;
  - a fixed-lane shift writes `null`.
- [ ] Dumping `http://localhost:8000/site/index.html?tree=../fixtures/tree.json&status=../fixtures/status.json&now=2026-09-28T13:14:00Z` shows the line `Auto lanes: 4 - lanes 3 -> 4 (weekly pace allows 5.2), 13:05 UTC` in `section#lanes`, before the first lane card.
- [ ] With a fixture copy whose `autoLanes` is `null`, and with one that lacks the field, the line is absent and the lane cards still draw.
- [ ] BL-761's lane-card checks still pass against the updated fixture.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`, and `-TestAutoLanes` still exits 0.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` and files under `.github/board` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
