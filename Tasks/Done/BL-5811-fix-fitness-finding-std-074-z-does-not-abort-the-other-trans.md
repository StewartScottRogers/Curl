---
id: BL-5811
title: Fix fitness finding STD-074: -Z does not abort the other transfers when a TELNET -t option fails at setup with exit 43 (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5811 — Fix fitness finding STD-074: -Z does not abort the other transfers when a TELNET -t option fails at setup with exit 43 (STILL OPEN, partly fixed)

## Goal

STD-074 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: -Z does not abort the other transfers when a TELNET -t option fails at setup with exit 43 (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** parallel transfers, TELNET option validation, Windows. The RTSP `Session:` (43) and unknown `-t` (48) cases are fixed.
- **Repro:** any TCP server on 127.0.0.1:25120; `python -m http.server 25080 --bind 127.0.0.1` serving a file `hello` containing `hello\n`.
  1. `curl -sS -m 5 -Z telnet://127.0.0.1:25120 -t NEW_ENV=nocomma -w "[%{exitcode}]\n" --next http://127.0.0.1:25080/hello -w "[%{exitcode}]\n"`
  2. The same with `-t TTYPE=` followed by 200 `x` characters.
- **Expected (REF):** stdout `[43]` then `[42]`, no `hello`; stderr `curl: (43) A libcurl function was given a bad argument` then `curl: (42) Transfer aborted due to critical error in another transfer`; exit 43.
- **Actual (.NET):** stdout `hello`, `[0]`, `[43]`; stderr only the (43) line; exit 43. Without `-Z` both match.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` `FatalSetupFailureOf` / `UnknownTelnetOptionFailure` (~lines 2278–2315) detect only the unknown-option (48) case. Validate at setup what curl's `check_telnet_options` rejects with 43 (`NEW_ENV` without a comma, over-long `TTYPE`/`XDISPLOC`, …) and treat it as run-ending.
- **Acceptance check:** commands 1 and 2 each print `[43]` `[42]` and the (42) line, exit 43, and no `hello`.

## Acceptance criteria

- [x] commands 1 and 2 each print `[43]` `[42]` and the (42) line, exit 43, and no `hello`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Setup now returns exit 43 for NEW_ENV without a comma, TTYPE of 32+ chars and XDISPLOC of 128+ chars (curl buffer sizes; the 200-char repro is over both). Limits chosen from curl's buffer sizes, not re-measured. Pinned in CurlCommandRunnerFatalErrorTests. Quality run clean.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: f6f9f61b 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. -Z now ends the run with 43/42 for NEW_ENV without comma and over-long TTYPE/XDISPLOC; tests added
