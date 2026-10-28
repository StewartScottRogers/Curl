---
id: BL-3969
title: Close CGR-170: quality gate, repro evidence and ticked criteria for the -Z setup-failure fix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3968]
touches: [Curl.Console.UnitTests/Cgr170Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3969 — Close CGR-170: quality gate, repro evidence and ticked criteria for the -Z setup-failure fix

## Goal

BL-3699's remaining acceptance work is done: the CGR-170 repros are recorded against real curl and Curl, the quality gate holds, and the fix is documented.

## Context

Split from BL-3699 (over its cost cap), after BL-3968 lands the fix. Repro measurements are in BL-3699's Notes (Deferred); ADR-0866 records the decision.

## Acceptance criteria

- [x] Each of the three CGR-170 repro commands (with `file://` URLs and the closed port 22099 if the report's server is gone) gives the same exit code, stdout bytes, stderr text, files written and request bytes in real curl and Curl; commands and both results are recorded in Notes.
- [x] Repro variants that already matched curl still match, and the focused test in `Cgr170Tests.cs` fails without the fix and passes with it (ADR-0482).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` is within every gate, and ADR-0866 and its README row are present and true.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3699.
- 2026-10-28 (lane 5): Measured curl 8.22.0 (`/ucrt64/bin/curl`, Schannel) against Curl at dd9574465 (`Curl.Console/bin/Debug/net10.0/curl.dll`), each run in a fresh folder holding `u1.txt`, with `H` = `http://127.0.0.1:22099` (closed, so neither sends a request byte) and `F` = a `file:///` URL of a local `in.txt`. Same exit code, stdout bytes, stderr text and files left (only `u1.txt`) in every case; the only difference anywhere is the connect time in `after N ms`.
  - `-Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{url}\n" H/ok1 H/ok2 "http://[::1"`: exit 3, stdout `0 3 H/ok1\n1 3 H/ok2\n`, stderr the three `bad range specification in position 9` lines then `curl: (3) URL using bad/illegal format or missing URL` twice.
  - `-Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code}\n" -T u1.txt H/a -T nope.txt H/b`: exit 26, stdout `0 26 000\n1 26 000\n`, stderr `cannot open 'nope.txt'`, try-help, `curl: (26) Failed to open/read local data from file/application` twice.
  - `-Z --parallel-max 1 -sS -w "%{urlnum} %{exitcode} %{url}\n" F?1 F?2 "http://[::1"`: exit 3, both file transfers run, then the glob lines, `2 3 http://[::1` and `curl: (3) URL rejected: Bad IPv6 address`.
  - Variants, also identical: `--parallel-max 1 F?1 "http://[::1" F?3` (abandons the batch), `--parallel-max 1 F?1 F?2 "F?{a"` (taken as written), `--parallel-max 1 -T u1.txt H/a -T u1.txt H/b -T nope.txt H/c` (`0 7`, `1 7`, `2 26`, exit 26), and the serial `-sS H/ok1 "http://[::1"` (exit 3) and `-sS -T u1.txt H/a -T nope.txt H/b` (exit 7, BL-440's text).
- Measured with a small PowerShell script outside the repository rather than Record-CurlExchange.ps1: the port is closed, so there is no exchange to record and no request bytes on either side.
- Without the fix: restoring the pre-fix files was refused by the sandbox (as in BL-3699), so the hold was switched off in place (`StartInParallelAsync`'s `run.HoldsFirstBatch` test made never true) and reverted: 4 of the 7 `Cgr170Tests` fail (the bad glob and unreadable `-T` cases in the first batch, and the bad glob past it); all 7 pass with the fix.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members (Curl.Console.UnitTests 3056 passed). ADR-0866 was renumbered ADR-0954 by the shift (c7d5b2f99, ADR-0818: another lane took 0866); ADR-0954 and its README row are present and true of `ParallelRun` (`HoldInFirstBatch`, `StartHeldAsync`, `AbandonFirstBatchAsync`, `SetupFailure`) and `CurlCommandRunner`.
- `dotnet build`: 0 warnings, 0 errors; fast tests: 46 test assemblies passed, none failed. CI on Linux and macOS: no code changed in this run, and the change BL-3968 landed touches no path or OS error text; platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fee2d392 e7b5cc53 36d6f07e 779790c0.
- 2026-10-28: Doing -> Done. CGR-170 repros match curl 8.22.0 byte for byte, Cgr170Tests catch the fix's removal, Curl.Console within every quality gate
