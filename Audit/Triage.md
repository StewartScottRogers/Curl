# Triage: from findings to Curl tasks

How a finding the audit office raised becomes work for the factory (ADR-0267, BL-1018). In
order:

1. **Findings arrive `proposed`**, on the `audit` branch, in the audit pull request. Each is one
   file in `Audit/Findings/` ([README](Findings/README.md)).
2. **Stewart decides each one.** He sets `status: accepted` or `status: rejected` in the
   finding's file - in the pull request, or by telling an interactive session which to set.
   Only Stewart accepts or rejects a finding; Claude never sets either status on its own
   judgement, however clear a finding looks.
3. **Accepted findings become tasks.** After the pull request has merged to `master`, an
   interactive session on `master`, or on `work/dark-factory` once it has merged `master`, runs

   ```powershell
   powershell -NoProfile -File Audit/Tools/New-TasksFromAcceptedFindings.ps1
   ```

   which files one Curl task per accepted finding that has none yet and writes the task's ID into
   the finding's `task` field. The session commits the new tasks and the updated findings, and
   pushes. `-WhatIf` shows what it would file without changing anything. The script refuses to
   run inside a dark factory process.
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
