---
id: BL-5442
title: Fix fitness finding STD-124: -F with a missing file: %{conn_id}/%{xfer_id} are 0 instead of -1, and a --next run continues and exits 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5442 — Fix fitness finding STD-124: -F with a missing file: %{conn_id}/%{xfer_id} are 0 instead of -1, and a --next run continues and exits 0

## Goal

STD-124 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: -F with a missing file: %{conn_id}/%{xfer_id} are 0 instead of -1, and a --next run continues and exits 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (the run's exit code hides the failed upload)
- **Confidence:** Confirmed (3 runs Windows, 1 Linux)
- **Protocol/area:** HTTP multipart early failure, write-out, `--next`
- **Repro:** HTTP server on 127.0.0.1:43000 serving `/echo/` and `/hello` (body `hello`); no `nofile.txt` in the working directory.
  1. `curl -sS -F f=@nofile.txt -w "%{conn_id} %{xfer_id} %{exitcode}\n" http://127.0.0.1:43000/echo/` (same with `-F 'f=<nofile.txt'`)
  2. `curl -sS -F f=@nofile.txt -w "%{conn_id} %{xfer_id} %{exitcode}\n" http://127.0.0.1:43000/echo/ --next -w "%{conn_id} %{xfer_id} %{exitcode}\n" http://127.0.0.1:43000/hello`
- **Expected (REF/REF2):** (1) `-1 -1 26`, exit 26. (2) `-1 -1 26`, the run stops, exit 26.
- **Actual (PORT):** (1) `0 0 26`. (2) `0 0 26`, `hello`, `1 1 0`, exit **0**. `-T nofile.txt` already matches REF.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` — the `-F` failure is not the `CannotOpenUploadFileFailure` that stops `TransferAllAsync` and that `TakeConnectionId` / `UsedAConnection` map to -1 (the STD-110 fix covers `-T` only).
- **Acceptance check:** (1) prints `-1 -1 26`; (2) prints only `-1 -1 26` and exits 26.

## Acceptance criteria

- [ ] (1) prints `-1 -1 26`; (2) prints only `-1 -1 26` and exits 26.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-124 (Medium).