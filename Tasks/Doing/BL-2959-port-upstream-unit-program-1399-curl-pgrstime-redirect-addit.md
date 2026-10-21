---
id: BL-2959
title: Port upstream unit program 1399 (Curl_pgrsTime redirect-additive timers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit1399, Curl.Conformance.UnitProgram.UnitTests/Limits/Unit1399, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitLibrary/RedirectHopTimings.cs, Curl.Core.UnitTests/RedirectHopTimingsTests.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2959 — Port upstream unit program 1399 (Curl_pgrsTime redirect-additive timers)

## Goal

Upstream case 1399 runs as a ported unit program over the Curl code that accumulates a transfer's phase timers across redirects, ends `Passed`, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2848 (ADR-0711 row 1399). `tests/unit/unit1399.c` of curl 8.22.0 (https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1399.c) calls `Curl_pgrsTime` for `TIMER_NAMELOOKUP`, `CONNECT`, `APPCONNECT`, `PRETRANSFER` and `STARTTRANSFER` 2 s after the start, then simulates a redirect (1 s spent redirecting, a new single-transfer start) and calls them again, `STARTTRANSFER` twice, and checks with 10 `fail_unless` that each total is 2 s and then 3 s: the timers add up across redirect hops, and `t_starttransfer` is set only on the first call of a hop. Curl's counterpart is `TransferTimings` (`Curl.Protocol.Abstractions.UnitLibrary`) with its `RedirectDuration`, the redirect follower that sets it, and the write-out code in `Curl.Output.UnitLibrary` (`TransferWriteOutVariables`, `WriteOutTransferInfo`) that turns them into `%{time_namelookup}` and the rest. First find where the hops' timers are summed; if Curl does not add them as curl does, fix that with a focused unit test in that library (ADR-0482), add its folder to `touches`, then port the program in `Curl.Conformance.UnitProgram.UnitLibrary/Limits/` (an `IUnitProgram` registered in `UnitPrograms.Limits.cs`, `UnitChecks` from `Hashes`), as BL-2848 ported 1605 and 1606.

## Acceptance criteria

- [x] `Unit1399` makes the C program's 10 checks in order (named in its doc comment), is registered in `UnitPrograms.Limits.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/Limits/`.
- [x] Case 1399 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1399'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit. (The port and the fix are pure computation on timestamps with no path, file or OS text; the shift's coordinator watches CI and files any failure.)

## Notes

- 2026-10-21 (lane 5): Curl was wrong. `RedirectFollower` measured the last hop's phase timestamps from the first hop's start, so after a redirect every phase also counted the earlier hops in full (4 s where unit1399 expects 3 s). curl's `Curl_pgrsTime` adds each hop's own phase time (`*delta += us` from `t_startsingle`). Fixed by the new `Curl.Core.UnitLibrary/RedirectHopTimings.cs` (sums each phase over the hops that reached it, as synthetic timestamps measured from the first start; `Started`, `Completed` and `RedirectDuration` unchanged), used by `RedirectFollower`'s chain. Recorded in ADR-0731 (decided by Claude under Stewart's delegation). Focused tests: `Curl.Core.UnitTests/RedirectHopTimingsTests.cs` and `RedirectFollowerTests.FollowAsync_TimedHops_AddsEachHopsTimeToFirstByte`.
- Touches widened to `Curl.Core.UnitLibrary/RedirectFollower.cs`, `RedirectHopTimings.cs` and their two test files for that fix; no task in Doing on `origin/work/dark-factory` names them.
- Port: `Limits/Unit1399/Unit1399RedirectAdditiveTimers.cs` drives `RedirectHopTimings` and `WriteOutTransferInfo.FromReport` on a `SteppedTimeProvider`; the 10 checks are name lookup, connect, app connect, pretransfer and start transfer at 2 s, then at 3 s, each preceded by the C program's `is N us same as S seconds? Yes` line. The C program's second `TIMER_STARTTRANSFER` call has no counterpart (one `FirstByteReceived` per hop), as the doc comment says. Registered by its qualified name in `UnitPrograms.Limits.cs` so no `using` line collides with BL-2958 or BL-2960.
- `CURL_UPSTREAM_CASE=1399` named-case test: Passed on Windows. Quality (`-Library Curl.Core.UnitLibrary,Curl.Conformance.UnitProgram.UnitLibrary`): both 100% line and branch, 0 failing members, worst CRAP 10 (`Merge` measured 20 from lifted `long?` additions until they moved into `StartedPlus`).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 8d87f494 5fcd55c5 c36de89e 8e1ce02b 620ebead.
