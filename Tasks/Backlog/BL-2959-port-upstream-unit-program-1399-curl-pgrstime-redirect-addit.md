---
id: BL-2959
title: Port upstream unit program 1399 (Curl_pgrsTime redirect-additive timers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit1399, Curl.Conformance.UnitProgram.UnitTests/Limits/Unit1399]
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

- [ ] `Unit1399` makes the C program's 10 checks in order (named in its doc comment), is registered in `UnitPrograms.Limits.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/Limits/`.
- [ ] Case 1399 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1399'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-21: Created.
