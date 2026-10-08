# Gap triage: how findings become tasks

ADR-0433 decisions 4 and 5. Claude accepts the gap analysis office's suggestions and files
the tasks itself; Stewart's approval is not needed. The finding format is in
[`Instructions/Gap-Format.md`](Instructions/Gap-Format.md), section 7, and
[`Findings/README.md`](Findings/README.md).

1. **Findings arrive `open`.** `Tools/Write-GapFindings.ps1` files each cause of a gap as
   `Findings/GF-####-*.md` with `status: open`, which means accepted.
2. **`Tools/New-TasksFromGaps.ps1` files their tasks.** For each open `scope: target`
   finding with no open task, it files one lane-eligible task, `Close GF-####: <title>`, on
   the factory's board with `task-board.ps1 new`, and writes the task's ID into the
   finding's `task`, `tasks` and Log. Priority comes from severity (Critical and High give
   High, Medium gives Normal, Low gives Low), the pipeline from the area (`protocols` gives
   `protocol`, any other `feature`), and `touches` from the finding with `Gap/`, `Audit/`
   and `.claude/agents/` paths dropped. The task stands alone: it carries the finding's
   Evidence and Suggestion, the targeted curl version and the upstream documents, because
   lanes cannot read `Gap/`. A `Close` or `Re-close` task filed by hand, its title naming
   the finding, counts as open, so nothing more is filed for that finding. The script
   refuses to run inside a dark factory lane.
3. **Stewart may reject any finding.** He alone sets `status: rejected`; a rejected finding
   gets no new task.
4. **A finding closes only on re-measurement.** A later gap run must measure every item as
   `match`, or `excluded` with a reason. A task reaching Done never closes a finding.
5. **When the fix does not hold, a `Re-close` task is filed.** When every task of an open
   finding is Done (in `Tasks/Done` or its archive) and the latest `Measurements` line,
   dated after the last completion, still shows gaps, the script files
   `Re-close GF-####: <title>`.
6. **`scope: newest` findings wait for a retarget ADR.** They get no task while Curl targets
   an older curl version; the ADR that moves `Baselines/target.json` makes them target
   findings (ADR-0433 decision 6).

Run it from an interactive session, after a gap run has written its findings:

```powershell
powershell -NoProfile -File Gap\Tools\New-TasksFromGaps.ps1 -WhatIf
powershell -NoProfile -File Gap\Tools\New-TasksFromGaps.ps1 -BoardRoot <work/dark-factory worktree>
```
