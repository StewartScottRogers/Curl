# Backlog

Tasks that are defined and waiting to be claimed. A task here is ready when it is
assigned to Claude and every task in its `depends-on` list is in `Done`.
`/task-run` takes ready tasks by priority, then by lowest ID.

This file is not a task; it keeps the folder in Git when the backlog is empty. See
`../README.md`.
