# Gap findings

One file per cause of a gap between Curl and upstream curl, `GF-####-<slug>.md`, filed by a
gap run (ADR-0433). Copy `FINDING-TEMPLATE.md` for the front matter and sections. Every
field is defined in [`../Instructions/Gap-Format.md`](../Instructions/Gap-Format.md),
section 7.

## Statuses

- `open`: filed and accepted. Claude files the tasks for a `scope: target` finding; a
  `scope: newest` finding gets none while Curl targets an older version.
- `closed`: a run measured every item as matching.
- `rejected`: set only by Stewart. A rejected finding gets no new tasks.

There is no won't-fix status.

## Closing and reopening (ADR-0433 decision 5)

- A finding closes only when a run measures every one of its items as `match`, or as
  `excluded` with a reason the run states. That run's stamp goes in `closed`.
- A finding never closes because its task reached Done. When every task is Done and the
  latest run still measures a gap, a `Re-close GF-####` task is filed.
- A `closed` finding whose item measures `gap` again reopens: `status: open`,
  `regression: true`, `closed` emptied, and the run that saw it written in the Log.

## Sections

`Summary`, `Evidence`, `Suggestion`, `Measurements` (one line per run: the stamp and how
many items are still gaps), `Log`, in that order.
