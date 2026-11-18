---
id: BL-5469
title: Fix fitness finding STD-074: -Z does not abort the other transfers when one fails at setup with exit 43 (RTSP) or 48 (TELNET) (STILL OPEN; TELNET 48 newly found)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5469 — Fix fitness finding STD-074: -Z does not abort the other transfers when one fails at setup with exit 43 (RTSP) or 48 (TELNET) (STILL OPEN; TELNET 48 newly found)

## Goal

STD-074 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: -Z does not abort the other transfers when one fails at setup with exit 43 (RTSP) or 48 (TELNET) (STILL OPEN; TELNET 48 newly found). Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs each)
- **Protocol/area:** parallel transfers (`-Z`), fatal errors, Windows
- **Repro:** an RTSP mock on 127.0.0.1:44542 answering every request `RTSP/1.0 200 OK` with the request's `CSeq`, `Session: 2`, `Content-Length: 0`; `python -m http.server 44080` serving a file `hello` (content `hello\n`); any TCP server on 127.0.0.1:44170.
  1. `curl -sS -Z rtsp://127.0.0.1:44542/m -H "Session: 1" --next http://127.0.0.1:44080/hello -w "[%{exitcode}]\n"`
  2. `curl -sS -Z http://127.0.0.1:44080/hello -w "[%{exitcode}]\n" --next rtsp://127.0.0.1:44542/m -H "Session: 1" -w "[%{exitcode}]\n"`
  3. `curl -sS -m 5 -Z telnet://127.0.0.1:44170 -t BOGUS=1 -w "[%{exitcode}]\n" --next http://127.0.0.1:44080/hello -w "[%{exitcode}]\n" </dev/null` (same with `--fail-early`)
- **Expected (REF):** (1) stdout `[42]`; stderr `curl: (43) Session ID cannot be set as a custom header.` then `curl: (42) Transfer aborted due to critical error in another transfer`; exit 43. (2) `[43]` `[42]`, no `hello`. (3) `[48]` `[42]` and the (42) line, exit 48. With `--parallel-max 1` REF runs the HTTP transfer first, and the PORT matches that.
- **Actual (PORT):** the HTTP transfer runs and prints `hello` and `[0]`; no (42) line; exit 43 / 48.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs` (~lines 2309, 2511, 3904, 3925: `IsFatalFailure`, `RecordEnd`). curl detects these errors while setting transfers up, before any starts. Validate the RTSP `Session:` header and the `-t` options at setup and treat a failure there as run-ending.
- **Acceptance check:** (1) prints `[42]` and the (42) line, exit 43; (3) prints `[48]` `[42]`, exit 48; neither prints `hello`.

## Acceptance criteria

- [ ] (1) prints `[42]` and the (42) line, exit 43; (3) prints `[48]` `[42]`, exit 48; neither prints `hello`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-074 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 9c2a20c6 69116afe 3d836d5e.
