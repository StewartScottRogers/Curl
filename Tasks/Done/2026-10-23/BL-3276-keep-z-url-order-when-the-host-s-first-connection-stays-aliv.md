---
id: BL-3276
title: Keep -Z URL order when the host's first connection stays alive, as ADR-0786 decides
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelHostQueue.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3276 — Keep -Z URL order when the host's first connection stays alive, as ADR-0786 decides

## Goal

A `-Z` run against a keep-alive server starts a host's transfers in URL order at `--parallel-max` 50 and 1, as curl 8.22.0 does, and moves the first waiter last only when the host's first connection was closed.

## Context

Found by the shift-end review (ADR-0710) of 6b344d38b...af2f6a9bd, code-reviewer.

ADR-0786 (`Documentation/Planning/Decisions/ADR-0786-z-order-keep-alive-extension-first-waiter-last-only-after-the-first-connection-closed.md`, BL-3255) is Accepted and says the first waiter goes behind the others only when the host's first connection was closed; with keep-alive the transfers start in URL order. Its Consequences say tests pin URL order with a keep-alive server at 50 and 1.

The code does not do this: `Curl.Console/ParallelHostQueue.cs` `Leave` (around line 74) always picks `pending` when the host's first transfer ends, and `FreeWaiting` (around line 98) always moves it to the back. Nothing reads whether the connection was kept alive, and no keep-alive test was added. So against a keep-alive server Curl reorders where curl keeps URL order, and the ADR states as current behaviour something the code lacks ("say what it does, do what it says").

## Acceptance criteria

- [x] `ParallelHostQueue.Leave` (or its caller) learns whether the ended transfer's connection stayed open, and moves the first waiter last only when it was closed.
- [x] A unit test pins URL order for a keep-alive host at `--parallel-max` 50 and at 1; the existing closing-server tests (ADR-0768 order) still pass.
- [x] No test pins the order for keep-alive at `--parallel-max` 2.
- [x] ADR-0786 matches the code once done; `dotnet build` is clean and the fast tests pass.

## Notes

- touches widened to Curl.Console/CurlCommandRunner.cs (the caller of Leave); no other Doing task names it.
- Keep-alive is read from the result report (HTTP version and Connection header); ADR-0786 records this. Measure-CodeQuality Curl.Console: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 2dd00fe0 a9f0f029 b47b94ac 20f2597e.
- 2026-10-23: Doing -> Done. Leave moves the first -Z waiter last only when the host's first connection closed; keep-alive keeps URL order
