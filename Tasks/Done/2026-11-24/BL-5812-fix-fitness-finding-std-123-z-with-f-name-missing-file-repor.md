---
id: BL-5812
title: Fix fitness finding STD-123: -Z with -F name=@missing-file reports a separate (26) failure for every URL instead of stopping after the first (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-24
---
# BL-5812 — Fix fitness finding STD-123: -Z with -F name=@missing-file reports a separate (26) failure for every URL instead of stopping after the first (STILL OPEN, partly fixed)

## Goal

STD-123 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: -Z with -F name=@missing-file reports a separate (26) failure for every URL instead of stopping after the first (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (was Medium; no request is sent any more)
- **Confidence:** Confirmed (3 runs Windows, 1 run Linux vs REF2)
- **Protocol/area:** HTTP multipart with `-Z`. The `--next` and serial forms are fixed.
- **Repro:** an HTTP server on 127.0.0.1:23000 answering any path; in a directory without `nofile.txt`:
  `curl -sS -F a=b -F f=@nofile.txt -Z http://127.0.0.1:23000/echo/a http://127.0.0.1:23000/echo/b http://127.0.0.1:23000/echo/c -w "[%{url} %{exitcode}]\n"`
  (Same with `-F 'f=<nofile.txt'`, `--parallel-max 1` and `--fail-early`.)
- **Expected (REF):** one `curl: (26) Failed to open/read local data from file/application` line; stdout only `[http://127.0.0.1:23000/echo/a 26]`; exit 26.
- **Actual (.NET):** three (26) lines and `[…/echo/a 26]`, `[…/echo/b 26]`, `[…/echo/c 26]`; exit 26; no request reaches the server. `-T nofile.txt` with `-Z` matches REF.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` `FormSetupFailureOf` (~line 4737, called ~4613) turns the form failure into `CannotOpenUploadFileFailure`, but the parallel scheduler does not end the run as it does for `-T` (`AbandonFirstBatchAsync`, ~line 2248).
- **Acceptance check:** the command prints exactly `[http://127.0.0.1:23000/echo/a 26]` and one (26) line, exit 26.

## Acceptance criteria

- [x] the command prints exactly `[http://127.0.0.1:23000/echo/a 26]` and one (26) line, exit 26.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fixed: a -F file that cannot be opened abandons the -Z first batch but reports only the first held transfer (ParallelRun.AbandonFirstBatchAsync reportsOnlyFirst), matching REF: one (26) line, one -w line. Updated Cgr175Tests pin (was three reports). Quality run clean. CI platforms: change is platform-neutral.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 54dfb14c 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Fixed: -Z -F missing file reports once
