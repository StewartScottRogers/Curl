# ADR-0407 — The run cost cap follows 2.7 times the median recent run

- **Status:** Accepted
- **Date:** 2026-10-03

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1377.

## Context

ADR-0288 capped every headless run at a fixed $6, chosen when the median task cost about
$2.36. Audit finding AF-0033 (process auditor, accepted by Stewart) measured the median
on 2026-10-03 at about $1.32, so the "three times the median" bar had fallen to about
$3.96, and BL-1284 ($5.39), BL-1320 ($4.26) and BL-1287 ($4.16) all passed it while
staying under the $6 cap. A fixed cap goes stale as the median moves. The newest 40 task
runs in `Curl.logs` had a median of $1.04 when this was written.

## Decision

1. Before each headless run, `RunDarkFactory.ps1` reads `total_cost_usd` from the
   `result` event of the newest 40 task run logs in its log folder (`BL-n-<stamp>[-Ln].jsonl`
   and their `-resumed` runs; `-resolve` runs are not tasks) and caps the run at 2.7 times
   their median (`Get-RunBudgetUsd`).
2. 2.7 rather than 3: claude checks `--max-budget-usd` between turns, so a run can end one
   turn's cost above the cap (ADR-0288); 10% of headroom keeps such a run under three
   times the median.
3. `-TaskBudgetUsd` (default 6, 0 for no cap) stays as the ceiling: the cap never rises
   above it. The cap never drops below $2, so a run of cheap tasks cannot starve the next
   one, and with fewer than 10 logged runs the cap is `-TaskBudgetUsd` itself.
4. A run stopped at the cap is handled as ADR-0288 says; its Blocked reason names the cap
   it hit.

## Consequences

- Task runs stay under three times the median as the median moves, which is what the
  process auditor measures.
- More tasks will reach the cap and go to Blocked for splitting than under the fixed $6;
  that is the intended signal that a task is too big for one run.
- `-TestTaskBudget` proves the arithmetic and the log reading.

## Alternatives considered

- **Lower the fixed default to $3.50.** Lost: it goes stale the same way the $6 did.
- **Cap at 3 times the median.** Lost: a run ends one turn above the cap and would pass
  the bar.
