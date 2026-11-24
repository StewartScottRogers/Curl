---
id: BL-5817
title: Fix fitness finding STD-169: -Z --parallel-max 1 with a missing -T file in a later --next group does not run the earlier transfer, and prints (26) twice
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5817 — Fix fitness finding STD-169: -Z --parallel-max 1 with a missing -T file in a later --next group does not run the earlier transfer, and prints (26) twice

## Goal

STD-169 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: -Z --parallel-max 1 with a missing -T file in a later --next group does not run the earlier transfer, and prints (26) twice. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Probable (deterministic in 2 runs; REF's own behaviour here is timing-dependent without `--parallel-max 1`)
- **Protocol/area:** `-Z`, `--next`, `-T`
- **Repro:** HTTP server on 127.0.0.1:23000 with `/hello` returning `hello\n`. `curl -sS -Z --parallel-max 1 http://127.0.0.1:23000/hello --next -T nofile.txt http://127.0.0.1:23000/echo/ --next http://127.0.0.1:23000/json -w "[%{exitcode}]\n"`
- **Expected (REF):** stdout `hello`; stderr `curl: cannot open 'nofile.txt'`, the `try 'curl --help'` line and one `curl: (26) Failed to open/read local data from file/application`; exit 26.
- **Actual (.NET):** empty stdout; the same stderr but with the (26) line twice; exit 26.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` `AbandonFirstBatchAsync` (~line 2248) and the `-T` pre-open check abandon transfers REF has already started.
- **Acceptance check:** stdout is `hello` and stderr has exactly one (26) line.

## Acceptance criteria

- [x] stdout is `hello` and stderr has exactly one (26) line.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Re-reported in fitness review 2026-11-24T0708 (commit `1e5d591`, the `preview` release binaries) as ``-Z --parallel-max 1` with a missing `-T` file in a later `--next` group skips the earlier transfer and prints (26) twice (STILL OPEN, unchanged)`. Where it differs from the Context above, this report wins:


- **Severity:** Low · **Confidence:** Confirmed · **Area:** `-Z` / `--next` / `-T`.
- **Repro:** `curl -sS -Z --parallel-max 1 http://127.0.0.1:23000/hello --next -T nofile.txt http://127.0.0.1:23000/echo/ --next http://127.0.0.1:23000/json -w "[%{exitcode}]\n"` (`/hello` returns `hello`).
- **Expected (REF 8.22):** stdout `hello`; stderr `curl: cannot open 'nofile.txt'`, `curl: try 'curl --help' or 'curl --manual' for more information`, one `curl: (26) Failed to open/read local data from file/application`; exit 26.
- **Actual (.NET):** empty stdout, `(26)` printed twice, exit 26. The hello transfer never runs.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` `HoldInFirstBatchAsync`: the first batch holds `--parallel-max` + 1 transfers and `AbandonFirstBatchAsync(CannotOpenUploadFileFailure)` abandons the valid one too.
- **Acceptance check:** stdout `hello`, exactly one `(26)` line, exit 26.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Notes updated from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 2227ebf4 69116afe 3d836d5e.
- 2026-11-24: Fixed: when the -T file of the first batch's extra transfer (past --parallel-max) cannot be opened, the transfers within --parallel-max start and only the failing one is reported, as for STD-183 (ParallelRun.AbandonFirstBatchAsync, startsOthersWhenExtraFails). Unit test added; Measure-CodeQuality clean for Curl.Console.
