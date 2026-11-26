---
id: BL-6036
title: Under -Z report a later --next group's malformed --resolve failure before a running transfer's body, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6036 — Under -Z report a later --next group's malformed --resolve failure before a running transfer's body, as curl does

## Goal

Under `-Z`, a later `--next` group's transfer that fails at its start with a malformed `--resolve` entry writes its `-w` output before the first group's still-running transfer writes its body, as curl 8.22.0 does.

## Context

Measured while delivering BL-6022 (2026-11-26) with `Record-CurlExchange.ps1 -Port 24000 -HoldOpenMilliseconds 1500 -AnswerHeldRequests 3`, command line
`-sS -Z http://127.0.0.1:24000/hello --next -sS --resolve bad -w "[%{exitcode}]\n" http://127.0.0.1:24000/json`.
Real curl 8.22.0 Schannel wrote stdout `[49]`, newline, `hello`; Curl wrote `hello[49]`, newline. Exit code (49) and stderr already match. Start in `Curl.Console/ParallelRun.cs`.

## Acceptance criteria

- [ ] The command line above writes `[49]` and a newline before `hello` on standard output, still with exit 49 and curl's stderr line.
- [ ] A test in `Curl.Console.UnitTests` pins that order without depending on the platform's scheduling.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
