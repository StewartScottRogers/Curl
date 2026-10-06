---
id: BL-518
title: Decide how parallel transfers run, write their output and report failures
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-509]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-518 — Decide how parallel transfers run, write their output and report failures

## Goal

An ADR fixes how `CurlCommandRunner` runs transfers concurrently under `-Z`: the scheduling model, how each transfer's output, `-w`, `-v` and error lines are ordered on the shared standard output and standard error, which exit code the run returns, and how `--fail-early` stops the rest, each matched to measured curl 8.21.0 behaviour.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker). Depends on BL-509 so the runner's group loop exists to build on.
- Code: `Curl.Console/CurlCommandRunner.cs`, `UrlTransfer.cs`, `TransferProgressRecorder.cs`, the connection pool (`Curl.Networking.UnitLibrary/PoolingConnector.cs`, ADR-0050) which must be safe for concurrent callers, and the cookie engine (`Curl.Console/CookieEngine.cs`).
- Measure before deciding, with `Record-CurlExchange.ps1 -Connections 3 -ResponseDelayMilliseconds` to make the order of completion differ from command-line order: stdout for three URLs to standard output, `-w '%{urlnum}\n'`, `-sS` with one failing URL, `--fail-early`, and the exit code when two fail differently.
- Rules: async all the way, inject `TimeProvider`, no `.Result`/`.Wait()`.

## Acceptance criteria

- [x] The measurements above are recorded in the ADR's Context (commands, stdout, stderr, exit codes).
- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed, stating the scheduling model, output ordering, exit-code rule, `--fail-early` rule, and what must be made safe for concurrent use (pool, cookie engine, progress).
- [x] The Consequences name the work BL-519, BL-520 and BL-521 do.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decided in ADR-0127: one command-line-ordered queue across groups, up to `--parallel-max` running as tasks; output in completion order under one write gate; exit code of the first failure in completion order (serial keeps the last); `--fail-early` aborts running transfers with 42 and ends queued ones with the first failure's code and curl's generic text.
- Measured with the recorder unchanged: it answers connections one after another, so completion order was made to differ from command-line order with a `file://` URL (instant), a missing file (instant, 37) and a refused port (about 2 s on Windows, 7) mixed with delayed HTTP URLs. The first attempt used a `file://` path under `%TEMP%`, which holds a space (`Stewart Rogers`) and gave exit 3; the recorded runs use `C:\ProgramData\bl518\b.txt`.
- Measured beyond the task's list, because the ADR needs them: `--fail-early` with transfers still queued behind `--parallel-max` (they report the first failure's code), `-v` with and without `--parallel-immediate` (same-host transfers wait for the first connection without it; that is BL-520's).
- Wrote the ADR in-session rather than through `align-and-document`: the measurements were already in this session's context, and the task changes no code or names.
- Filed BL-755 (make `CookieStore` safe for concurrent transfers; it has no lock) and added it to BL-519's `depends-on`, since BL-519's context says concurrency work outside `Curl.Console` is its own task.
- ADR index: the README's table jumped from 0124 to 0127; rows for ADR-0125 and ADR-0126 were missing and were added from those ADRs' Decision sections (the README is inside this task's `touches`).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0127 fixes -Z scheduling, completion-order output, first-failure exit code and --fail-early from measured curl 8.21.0
