# ADR-0694: The board offers a task with narrow touches before one holding a whole project

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

On 2026-10-18, after ADR-0689, 38 of 59 ready tasks named the whole `Curl.Engine.UnitLibrary`
project. Twenty-one of them were then narrowed to the files they change. Capacity stayed at 2.

`task-board.ps1` orders ready tasks by priority, then by how many unfinished tasks wait on each,
then by lowest ID. A ready task that overlaps a task in `Doing` reserves its `touches`, so no later
task that overlaps it starts first (BL-1041). The low IDs mostly held the whole Engine project. So
the first of them reserved all of it, and every narrowed Engine task behind it was held back, even
those whose files no running task touches.

## Decision

Between the waiting count and the ID, ready tasks are ordered by `Get-TouchBreadth`: how many
whole folders their `touches` name. An entry with no `/` counts as one, `*` counts as more than any
list, and a shared list file (ADR-0689) counts for nothing. So tasks that name files in a busy
project come first, start side by side when their files differ, and reserve only those files.
`Test-TaskBoardOverlap.ps1` pins it: a task holding the whole Engine project, then two naming
different Engine files, give a capacity of 2, not 1.

## Consequences

- On the live board of 2026-10-18 the capacity went from 2 to 3. It grows as more tasks are
  narrowed, which `/task-run` now asks lanes to do once the code shows where a change lands.
- A task that holds a whole project waits while narrower tasks of the same priority and waiting
  count are ready. BL-1041's reservation still keeps it from waiting forever once it is first in
  order. Raising its priority, or narrowing it, moves it up.
- Priority and the waiting count still come first, so a High task, or one that others wait on,
  is never passed over for a narrower one.
