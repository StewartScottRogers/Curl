---
description: Archive finished tasks from Tasks/Done into a new timestamped folder.
argument-hint: [days] — archive tasks completed at least this many days ago (default 7; 0 archives all of Done)
---
Archive the task board's `Done` folder. Days: **$ARGUMENTS** (if blank, use 7).

Run
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 archive -OlderThanDays <days>`.

Report the folder it created and the task IDs it moved, or that there was nothing
old enough to archive. Pass on any warning about a task with no `completed` date.

Archived tasks are final: never edit a file inside `Tasks/Done/<timestamp>/`
afterwards. They still count as done for other tasks' dependencies.
