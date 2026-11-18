---
id: BL-5441
title: Fix fitness finding STD-123: -Z with -F name=@missing-file sends empty POST requests to every URL but one, and those transfers report success
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5441 — Fix fitness finding STD-123: -Z with -F name=@missing-file sends empty POST requests to every URL but one, and those transfers report success

## Goal

STD-123 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: -Z with -F name=@missing-file sends empty POST requests to every URL but one, and those transfers report success. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (silent wrong operation: requests curl never sends reach the server, and the form is dropped)
- **Confidence:** Confirmed (4 runs Windows, 1 Linux)
- **Protocol/area:** HTTP multipart, `-Z` parallel; Windows and Linux builds
- **Repro:** an HTTP server on 127.0.0.1:43000 whose `/echo/*` path echoes the request line, headers and body. In a directory without `nofile.txt`:
  `curl -sS -F a=b -F f=@nofile.txt -Z http://127.0.0.1:43000/echo/a http://127.0.0.1:43000/echo/b http://127.0.0.1:43000/echo/c -w "[%{url} %{exitcode}]\n"`
  Also: `curl -sS -F f=@nofile.txt -Z http://127.0.0.1:43000/echo/a --next -F f=@have.txt http://127.0.0.1:43000/echo/b` (with `have.txt` present).
- **Expected (REF and REF2):** nothing is sent. `curl: (26) Failed to open/read local data from file/application` and `[http://127.0.0.1:43000/echo/a 26]`, exit 26. In the `--next` form, nothing runs.
- **Actual (PORT):** `POST /echo/a` and `POST /echo/b` with `Content-Length: 0` and no multipart Content-Type reach the server and print `[… 0]`; only `/echo/c` gets `curl: (26) …`; exit 26. In the `--next` form the second, valid upload is performed.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs`. The `-F` open failure from `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs` (~line 511, `OpenFailedMessage`) is not handled like the `-T` failure (`CannotOpenUploadFileFailure`, ~line 660).
- **Acceptance check:** the first command sends no request (server log empty), prints exactly `[http://127.0.0.1:43000/echo/a 26]`, exit 26.

## Acceptance criteria

- [ ] the first command sends no request (server log empty), prints exactly `[http://127.0.0.1:43000/echo/a 26]`, exit 26.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-123 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 0b33b67a 69116afe 3d836d5e.
