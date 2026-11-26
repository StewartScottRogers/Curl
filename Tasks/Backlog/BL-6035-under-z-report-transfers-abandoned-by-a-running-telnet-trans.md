---
id: BL-6035
title: Under -Z, report transfers abandoned by a running telnet transfer's fatal failure as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelTelnetTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6035 — Under -Z, report transfers abandoned by a running telnet transfer's fatal failure as curl does

## Goal

When a telnet transfer under `-Z` fails with exit 43 during its session, the HTTP transfer waiting behind it is reported as curl 8.22.0 reports it.

## Context

Found by BL-6017 (ADR-1420). Measured on curl 8.22.0 Schannel on Windows, 2026-11-26: a loopback telnet server
sends `IAC DO NEW-ENVIRON`, `IAC DO TTYPE`, then `SB NEW-ENVIRON SEND`, `SB TTYPE SEND` and `SB XDISPLOC SEND`;
command `-sS -m 5 -Z [--parallel-max 1] -w "[%{exitcode}]\n" telnet://... -t NEW_ENV=nocomma --next -sS -w
"[%{exitcode}]\n" http://.../hello`, standard input `NUL` (the same for a 40-character `TTYPE`).

- With `--parallel-max 1`: curl prints two `curl: (43)` lines and `[43]` `[43]`; Curl prints `(43)` then
  `(42)` and `[43]` `[42]`.
- Without it: curl prints `(43)`, `(42)`, `[43]`, then `hello` and `[42]`; Curl prints the same codes
  without `hello`. Measure the order of the two streams separately (the run merged them).

## Acceptance criteria

- [ ] With `--parallel-max 1` the command above reports both transfers with exit 43, as measured on curl.
- [ ] Without `--parallel-max` the command's standard output, standard error and exit code match a fresh curl measurement, or an ADR records why Curl differs.
- [ ] A focused unit test in `Curl.Console.UnitTests` pins the behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
