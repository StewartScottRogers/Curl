# ADR-0710: After each shift-end merge, five read-only reviewers read what the shift merged and file tasks

- Status: Trial (2026-10-19): Stewart asked for two shifts' runs, then a decision on keeping it.
  He fears it takes too long and repeats what the audit office already does.
  `$ShiftReviewTrialShifts` (2) stops the review after two reports, until he decides.
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation. Stewart chose this option (one review per shift) over a review of every lane task, or none.

## Context

No reviewer read the dark factory's code before it reached `master`. A lane builds, runs the fast
tests and the quality gates, and CI runs on Windows, Linux and macOS. `/task-run` has no review
step, and the audit office audits only now and then. The PR Review Toolkit plugin (Anthropic's
official marketplace) has six review agents: code, tests, comments, error handling, type design and
a code simplifier. Running them on every lane task (about 60 a day) would cost six reviews a task.

## Decision

- `RunDarkFactory.ps1`'s `Invoke-ShiftReview` runs once per shift, right after the shift-end merge.
  It is one headless `claude -p --model opus` run in a worktree detached at the merged commit, over
  `git diff <master before the merge>...<merged commit>`. It is capped at `$ShiftReviewBudgetUsd`
  ($15) and `$ShiftReviewMinutes` (45).
- It runs `/pr-review-toolkit:review-pr errors types code parallel`: silent failures, type design,
  and bugs plus CLAUDE.md compliance. Changed on 2026-10-19 at Stewart's request, before the trial's
  first run, from all five reviewers that only read. The audit office already covers tests
  (`audit-quality`: weak assertions, surviving mutants) and comments (`audit-truthfulness`: names
  and doc comments against the code), so those two are left out. `code-simplifier` rewrites code,
  which a review must not do, so it is left out too.
- Under `-Continuous` with work ready, the ended shift starts the next shift first, then runs the
  review in its own process beside it. It publishes no heartbeat while it does, and
  `-Restart` picks the newest coordinator. So nothing waits for the review, and lanes claim what it
  files. With nothing ready, the review runs first, and the next shift starts if it filed work.
- For each Critical issue it files a High task, and for each Important issue a Normal task, with the
  board script. It files none for Suggestions, and none that touches an audit path or a guard file.
  It pushes the tasks with `[skip ci]` and writes `<logs>/review-<stamp>.md`, ending
  `FILED: <n>`. The shift log traces one `review` line.
- It never holds or undoes the merge, since CI is the merge gate.
- It is skipped when the plugin is not installed (`claude plugin list`). On this machine the plugin
  is installed for the user (`claude plugin install pr-review-toolkit@claude-plugins-official`).

## Consequences

- Every shift's merged code is read by a reviewer once, for about $15 at most a shift, instead of six
  reviews a task.
- A defect it finds is already on `master`, and is fixed by the next shift.
- The next shift starts as soon as before. With nothing ready, an idle factory waits up to 45
  minutes for the review.
- A review run on a large shift reads the diff selectively. It finds the defects it reaches, not
  every defect.
