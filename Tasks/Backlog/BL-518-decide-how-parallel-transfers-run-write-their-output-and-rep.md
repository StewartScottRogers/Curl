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
completed:
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

- [ ] The measurements above are recorded in the ADR's Context (commands, stdout, stderr, exit codes).
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed, stating the scheduling model, output ordering, exit-code rule, `--fail-early` rule, and what must be made safe for concurrent use (pool, cookie engine, progress).
- [ ] The Consequences name the work BL-519, BL-520 and BL-521 do.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
