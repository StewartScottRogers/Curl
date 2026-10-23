---
id: BL-3153
title: Keep Complete-ClaimSplit from dropping a reset-failed split's stash copies while its commit stays to be pushed
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3153 — Keep Complete-ClaimSplit from dropping a reset-failed split's stash copies while its commit stays to be pushed

## Goal

When `Complete-ClaimSplit` handles `reset-failed` and its push is refused, it never leaves a split commit that will still reach origin without the stash copies its new tasks name. It also traces only what it actually did.

## Context

Found by the shift-end review of 2026-10-22 (ADR-0710), reviewer silent-failure-hunter, in `DarkFactory/Claim.ps1:219-234` (BL-3143, commit 3ef475374).

When `Invoke-TaskSplit` returns `reset-failed`, the split commit is still at HEAD in the lane's checkout. `Complete-ClaimSplit` retries one bare `push`, with no `pull --rebase` first. If that push is refused (usually because another lane has just pushed), the function drops the stash copies stored under the new IDs, on the grounds that "origin never saw them". It never removes the split commit itself.

The lane then goes on with its claimed task. Its later integration rebases and pushes the split commit after all. Origin then holds new tasks that say "Continues the stashed work of BL-x", but no stash exists under their IDs, so the lane that claims one starts from scratch. The stash list is shared by every worktree, so the drop is global.

The trace at line 232 is also wrong in two cases:

- If the subject regex at line 227 does not match, `$newIds` is empty and nothing is dropped. The trace still says "dropped the stash copies of " followed by nothing.
- `Remove-TaskStashEntries` only traces a failed drop and returns nothing, so line 232 says "dropped" even when a drop failed.

## Acceptance criteria

- [x] On `reset-failed`, a refused push is retried after `pull --rebase` (with `rebase --abort` only when a rebase is in progress), as the `failed` branch does.
- [x] Stash copies are dropped only when the split commit is no longer in the lane's history. Otherwise they are kept, and the trace says the split commit is still waiting to be integrated.
- [x] When the subject is not recognised, the trace says so and drops nothing. The "dropped" trace names only the entries that were actually removed.
- [x] `Claim.ps1`'s self-test (`-TestClaimDuplicates`) has a case for each of these, and they pass.

## Notes

On reset-failed the push is retried after pull --rebase (rebase --abort only while rebasing). The subject is read before pushing; stash copies are dropped only when the split commit is absent from origin/branch..HEAD, and the trace names only removed IDs. Trace texts are short because Write-Trace cuts lines at 118 characters. Self-test: 3 new cases in -TestClaimDuplicates, all pass.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 636e48fb a38c9b2e 188efd23 ffaf3fa6.
- 2026-10-22: Doing -> Done. Complete-ClaimSplit keeps stash copies while the reset-failed split commit awaits integration
