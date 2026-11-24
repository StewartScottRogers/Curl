---
id: BL-5851
title: Fix fitness finding STD-183: -Z --parallel-max with a TELNET -t or RTSP Session: setup failure crashes with "Index was out of range" (exit 2) and skips valid transfers
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5851 — Fix fitness finding STD-183: -Z --parallel-max with a TELNET -t or RTSP Session: setup failure crashes with "Index was out of range" (exit 2) and skips valid transfers

## Goal

STD-183 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: -Z --parallel-max with a TELNET -t or RTSP Session: setup failure crashes with "Index was out of range" (exit 2) and skips valid transfers. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (internal error; transfers REF completes are skipped) · **Confidence:** Confirmed (Windows and Linux builds) · **Area:** TELNET / RTSP setup failure under `-Z`. Next to this round's STD-074 fix.
- **Mocks:** a TCP server on 127.0.0.1:25120 that accepts telnet connections; an RTSP server on 25130; an HTTP server on 25080 serving `/hello` = `hello\n` (e.g. `python -m http.server 25080 --bind 127.0.0.1` in a directory containing `hello`).
- **Repro 1:** `curl -sS -m 5 -Z --parallel-max 1 -w "[%{exitcode}]\n" telnet://127.0.0.1:25120 -t NEW_ENV=nocomma --next -w "[%{exitcode}]\n" http://127.0.0.1:25080/hello`
  - **Expected (REF):** stdout `[43]` `[43]`; stderr `curl: (43) A libcurl function was given a bad argument` twice; exit 43.
  - **Actual (.NET):** stdout `[43]` `[42]`; stderr `(43)`, `curl: (42) Transfer aborted due to critical error in another transfer`, then `curl: (2) Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')`; exit 2.
- **Repro 2:** `curl -sS -m 5 -Z --parallel-max 1 -w "[%{exitcode}]\n" http://127.0.0.1:25080/hello --next -w "[%{exitcode}]\n" telnet://127.0.0.1:25120 -t NEW_ENV=x --next -w "[%{exitcode}]\n" http://127.0.0.1:25080/hello`
  - **Expected (REF):** `hello`, `[0]`, `[43]`, `[43]`; exit 43.
  - **Actual (.NET):** `[43]` `[42]` — the first, valid download never runs — plus the same "Index was out of range" error; exit 2.
- **Also:** `-t BOGUS=1 --parallel-max 1` (REF `[48][48]` exit 48; port exit 2); `--parallel-max 2` with three transfers (REF `[43][42][43]` exit 43; port exit 2); RTSP `... http://127.0.0.1:25080/hello --next -H "Session: x" rtsp://127.0.0.1:25130/s` (REF `hello [0] [43]` exit 43; port exit 2). Without `--parallel-max`, `-Z` matches REF.
- **Likely location:** `Curl.Console/ParallelRun.cs`: `NoteFatalSetupFailureOfLastHeld` stores `fatalSetupFailureIndex = heldTransfers.Count - 1` and `AbandonFirstBatchOnFatalFailureAsync` later indexes `heldTransfers[fatalSetupFailureIndex]` after the held list changed. `Curl.Console/CurlCommandRunner.cs` ~2245–2266 (`FatalSetupFailureOf`, `run.HoldInFirstBatch`) treats the failure as part of the first batch even when `--parallel-max` moved it later.
- **Acceptance check:** Repro 1 prints `[43]` `[43]`, stderr has no `Index was out of range`, exit 43. Repro 2 prints `hello` and `[0]` before `[43]`, exit 43.

## Acceptance criteria

- [x] Repro 1 prints `[43]` `[43]`, stderr has no `Index was out of range`, exit 43. Repro 2 prints `hello` and `[0]` before `[43]`, exit 43.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `ParallelRun.AbandonFirstBatchOnFatalFailureAsync` snapshots the held list; when the failing transfer is the extra one past `--parallel-max`, the others start and run on and the extra one is reported with the failure; otherwise the within-max ones are aborted. Repro 1 gives [43][43]; repro 2 gives hello [0] [43], exit 43.
- Known gap (not done): a transfer past the batch of --parallel-max+1 is not reported at all after the run ends (REF prints [43] for it too). Left as is; criteria only need hello, [0], [43].

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: c69094b9 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. STD-183 fixed: fatal setup failure past --parallel-max no longer crashes; tests and quality gate green
