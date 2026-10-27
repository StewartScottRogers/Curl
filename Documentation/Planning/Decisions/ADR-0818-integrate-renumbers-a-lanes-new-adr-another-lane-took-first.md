# ADR-0818: Integrating renumbers a lane's new ADR when the shared branch already uses its number

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3834
- Decided by Claude under Stewart's delegation.

## Context

Each lane picks the next ADR number from its own worktree, so lanes working at the same time pick the same one: by 08:37 on 2026-10-26 `Documentation/Planning/Decisions` held six ADR-0815s, seven ADR-0816s and four ADR-0817s. Task IDs have the same race and `task-board.ps1 dedupe -Since` settles it after the rebase; ADRs had nothing.

## Decision

`DarkFactory/AdrIds.ps1` (`Invoke-AdrRenumber`) runs in `Update-BoardBeforePush`, under the integrate lock, after the rebase and the task dedupe. An ADR file the lane's commits added (absent at `origin/<branch>`) whose number a file at the branch already has, or an earlier-named file the lane also added has, gets the next number above every ADR in the tree. Its file name changes and so does the old number on every line the lane added: its heading, its README row (`[0900](ADR-0900-...)`) and any task or document citing it. Lines the lane did not add are never rewritten, so another lane's row in the same README keeps its number. ADRs present at the branch keep their numbers even when they share one (BL-3835 cleans those up). The renumbering is one commit; a failure stops the push and is traced with its reason, as a failed task dedupe does (BL-3491).

## Consequences

- A lane that cites an ADR of the branch by a number it also renumbered is not a case: a lane only renumbers numbers the branch did not have when the lane chose it.
- A citation of the old number on a line the lane did not add (an edit of an old line) is left alone.
- `-TestAdrIds` proves it on scratch repositories.
