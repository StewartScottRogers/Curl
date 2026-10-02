# Triage: from findings to Curl tasks

How a finding the audit office raised becomes work for the factory (ADR-0267, BL-1018). In
order:

1. **Findings arrive `proposed`**, on the `audit` branch, in the audit pull request. Each is one
   file in `Audit/Findings/` ([README](Findings/README.md)).
2. **Stewart decides each one.** He accepts it, defers it to decide later, or blocks it until
   something else happens; a deferred finding goes back to `proposed` or is rejected, and a
   blocked one goes back to `proposed` ([Moves](Findings/README.md#moves)). Each move sets the
   finding's `status` and `reason` and adds a `## Log` line - on the board page's Audit tab
   (BL-1185), in the pull request, or by telling an interactive session which to set. Only
   Stewart makes these moves; Claude never makes one on its own judgement, however clear a
   finding looks. A deferred or blocked finding is not turned into a task.
3. **Accepted findings become tasks.** An interactive session sets the status on the `audit`
   branch, then runs the script from the audit branch's worktree against the factory's board:

   ```powershell
   $env:CLAUDE_PROJECT_DIR = '<the work/dark-factory checkout>'
   powershell -NoProfile -File Audit/Tools/New-TasksFromAcceptedFindings.ps1
   ```

   which files one Curl task per accepted finding that has none yet, on the factory's board, and
   writes the task's ID into the finding's `task` field, on the `audit` branch. The two land
   apart: the session commits and pushes the new tasks on `work/dark-factory`, and the updated
   findings on `audit`, whose pull request it merges once CI is green. Never commit a finding on
   `work/dark-factory` - the CI audit guard fails any audit path changed there. `-WhatIf` shows
   what it would file without changing anything. The script refuses to run inside a dark factory
   process.
4. **The factory works the tasks** like any other: they change product code, not audit paths, so
   lanes may take them. The finding stays `accepted` while its task is worked and after it is
   Done; only a later re-audit that finds the reproduction no longer reproduces closes it
   ([closure rule](Findings/README.md#rules)).

How a finding maps to its task:

| Finding | Task |
| --- | --- |
| Severity Critical or High | Priority High |
| Severity Medium | Priority Normal |
| Severity Low | Priority Low |
| Auditor truthfulness | Pipeline `docs` |
| Auditor process | Pipeline `direct` |
| Any other auditor | Pipeline `feature` |
| The project folders its location names (never an audit path) | `touches`; none when it names no repository folder, so the task runs alone |
