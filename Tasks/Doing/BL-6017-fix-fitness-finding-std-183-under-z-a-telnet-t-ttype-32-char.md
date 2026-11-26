---
id: BL-6017
title: Fix fitness finding STD-183: under -Z, a TELNET -t TTYPE=<32+ chars> or -t NEW_ENV=<no comma> fails at setup and aborts the other transfers (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6017 — Fix fitness finding STD-183: under -Z, a TELNET -t TTYPE=<32+ chars> or -t NEW_ENV=<no comma> fails at setup and aborts the other transfers (PARTLY FIXED)

## Goal

STD-183 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: under -Z, a TELNET -t TTYPE=<32+ chars> or -t NEW_ENV=<no comma> fails at setup and aborts the other transfers (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (4 variants, 2 mocks) · **Area:** TELNET / `-Z`, Windows build.
- **What is fixed:** the "Index was out of range" crash is gone.
- **Mock servers:** a TELNET server on `127.0.0.1:24121` that sends `IAC DO NEW-ENVIRON` and `IAC DO TTYPE` but never sends a sub-negotiation; an HTTP server on `127.0.0.1:24000` with `/hello` returning `hello`.
- **Repro:** `curl -sS -m 5 -Z -w "[%{exitcode}]\n" telnet://127.0.0.1:24121 -t TTYPE=0123456789012345678901234567890123456789 --next -w "[%{exitcode}]\n" http://127.0.0.1:24000/hello < /dev/null` (also with `-t NEW_ENV=nocomma`, and with `--parallel-max 1`).
- **Expected (REF):** `[0]`, `hello`, `[0]`, exit 0. (curl fails NEW_ENV-without-comma only when the server sends `SB NEW-ENVIRON SEND`; it never rejects a long TTYPE.)
- **Actual (.NET):** `curl: (43) A libcurl function was given a bad argument`, `[43]`, then `curl: (42) Transfer aborted due to critical error in another transfer`, `[42]`; exit 43. With `--parallel-max 1`: `[43]` `[43]`. Without `-Z` the port matches REF.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs`, `UnknownTelnetOptionFailure` / `BadTelnetOptionValueFailure` (~lines 2301–2336), applied by the `-Z` first-batch setup but not by the serial path.
- **Acceptance check:** the repro prints `[0]`, `hello`, `[0]` and exits 0.

## Acceptance criteria

- [ ] the repro prints `[0]`, `hello`, `[0]` and exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 289f15f7 69116afe 3d836d5e.
