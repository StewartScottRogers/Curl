---
id: BL-5465
title: Fix fitness finding STD-146: A rejected HTTP/0.9 reply (exit 1) reports %{conn_id} -1 and a non-zero %{time_starttransfer}
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5465 — Fix fitness finding STD-146: A rejected HTTP/0.9 reply (exit 1) reports %{conn_id} -1 and a non-zero %{time_starttransfer}

## Goal

STD-146 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: A rejected HTTP/0.9 reply (exit 1) reports %{conn_id} -1 and a non-zero %{time_starttransfer}. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTP/1.1 response parsing, write-out
- **Repro:** a TCP server on 127.0.0.1:43001 that reads the request, sends `garbage\r\n\r\n` and closes. `curl -s -o NUL http://127.0.0.1:43001/nostatus -w "%{exitcode} %{conn_id} %{time_starttransfer}\n"`
- **Expected (REF):** `curl: (1) Received HTTP/0.9 when not allowed`, `1 0 0.000000`.
- **Actual (PORT):** same stderr, `1 -1 0.0028…`.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` `UsedAConnection` treats every exit 1 as "no connection"; limit that to URL-rejection failures.
- **Acceptance check:** the command prints `1 0 0.000000`.

## Acceptance criteria

- [x] the command prints `1 0 0.000000`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix needed three files: the runner and the engine (ManagedEasyTransfer.Finish.cs) treated every exit 1 as no connection; now only an exit 1 other than the HTTP/0.9 refusal is. The HTTP handler drops FirstByteReceived for that refusal so time_starttransfer is 0. Repro prints `1 0 0.000000`. Quality run clean for the three libraries. No other Doing task touched these files.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-146 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 5d44ebd8 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Rejected HTTP/0.9 reply reports conn_id 0 and time_starttransfer 0
