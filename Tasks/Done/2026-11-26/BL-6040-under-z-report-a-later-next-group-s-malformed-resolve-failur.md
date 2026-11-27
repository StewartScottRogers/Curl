---
id: BL-6040
title: Under -Z report a later --next group's malformed --resolve failure before a running transfer's body, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs, Curl.Console/ParallelHostQueue.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6040 — Under -Z report a later --next group's malformed --resolve failure before a running transfer's body, as curl does

## Goal

Under `-Z`, a later `--next` group's transfer that fails at its start with a malformed `--resolve` entry writes its `-w` output before the first group's still-running transfer writes its body, as curl 8.22.0 does.

## Context

Measured while delivering BL-6022 (2026-11-26) with `Record-CurlExchange.ps1 -Port 24000 -HoldOpenMilliseconds 1500 -AnswerHeldRequests 3`, command line
`-sS -Z http://127.0.0.1:24000/hello --next -sS --resolve bad -w "[%{exitcode}]\n" http://127.0.0.1:24000/json`.
Real curl 8.22.0 Schannel wrote stdout `[49]`, newline, `hello`; Curl wrote `hello[49]`, newline. Exit code (49) and stderr already match. Start in `Curl.Console/ParallelRun.cs`.

## Acceptance criteria

- [x] The command line above writes `[49]` and a newline before `hello` on standard output, still with exit 49 and curl's stderr line.
- [x] A test in `Curl.Console.UnitTests` pins that order without depending on the platform's scheduling.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: the failing transfer waited in `ParallelHostQueue` for the host's first transfer to end, so it was
  reported after `hello`. curl fails a malformed `--resolve` entry before it looks for a connection, so such a
  transfer now neither waits for nor counts at its host (`CurlCommandRunner.TransferOnceHostIsFreeAsync`).
- Its end still frees transfers pending on a host's first connection (new `ParallelHostQueue.EndWithoutHost`).
  Measured, real curl 8.22.0 Schannel, 2026-11-26: `-v -Z .../a .../b --next -v --resolve bad -w ... .../c`,
  server answering after 500 ms: `/b` waits as a pipewait candidate, `/c` fails, then `/b` dials its own
  connection before `/a` is answered. Curl now does the same.
- A match of measured curl with no design choice, so no ADR.
- `touches` widened by `Curl.Console/ParallelHostQueue.cs`, which holds the wait; no other task in Doing on
  `origin/work/dark-factory` named it. `ParallelRun.cs` needed no change.
- Verified: the task's command line through `Record-CurlExchange.ps1` gives `[49]`, newline, `hello`, exit 49 and
  curl's stderr line; `dotnet build` clean; fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Console`
  reports 0 failing members. The new tests are platform-neutral (in-memory server, no files or paths).
- A recorder run with `-Connections 1` and two same-host URLs beside the bad one hung real curl until stopped,
  as nothing accepted the second connection: give such measurements `-m`.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: c97b4777 e9fa31d3 3d836d5e.
- 2026-11-26: Doing -> Done. Under -Z a malformed --resolve failure no longer waits for its host, so it is reported before a running transfer's body, as curl does
