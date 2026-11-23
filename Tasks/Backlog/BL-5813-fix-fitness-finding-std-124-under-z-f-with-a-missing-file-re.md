---
id: BL-5813
title: Fix fitness finding STD-124: under -Z, -F with a missing file reports %{conn_id}/%{xfer_id} 0, 1, 2 instead of -1 (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5813 — Fix fitness finding STD-124: under -Z, -F with a missing file reports %{conn_id}/%{xfer_id} 0, 1, 2 instead of -1 (STILL OPEN, partly fixed)

## Goal

STD-124 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: under -Z, -F with a missing file reports %{conn_id}/%{xfer_id} 0, 1, 2 instead of -1 (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (was Medium; the serial and `--next` exit codes are fixed)
- **Confidence:** Confirmed (2 runs Windows, 1 run Linux)
- **Protocol/area:** write-out with `-Z`
- **Repro:** `curl -sS -Z -F f=@nofile.txt http://127.0.0.1:23000/echo/a -w "%{http_code}|%{num_connects}|%{conn_id}|%{xfer_id}|%{errormsg}\n"`
- **Expected (REF):** `000|0|-1|-1|Failed to open/read local data from file/application`
- **Actual (.NET):** `000|0|0|0|Failed to open/read local data from file/application` (with three URLs: `0 0`, `1 1`, `2 2`). Without `-Z` it prints `-1|-1`, matching REF.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs`, the parallel path's id assignment (`TakeConnectionId` / `UsedAConnection`) does not recognise `CannotOpenUploadFileFailure` for `-F` as the serial path does.
- **Acceptance check:** the command prints `000|0|-1|-1|Failed to open/read local data from file/application`.

## Acceptance criteria

- [ ] the command prints `000|0|-1|-1|Failed to open/read local data from file/application`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.