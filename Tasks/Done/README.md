# Done

Finished tasks, each with a `completed` date and a one-line completion note in its
`Log`.

`/task-archive` moves older finished tasks into a timestamped subfolder,
`Done/<yyyy-MM-dd_HHmm>/`, one folder per archive run. Archived tasks are never
edited, but their IDs still count: they satisfy other tasks' dependencies, and the
ID sequence continues past them.

This file is not a task; it keeps the folder in Git when nothing is finished. See
`../README.md`.
