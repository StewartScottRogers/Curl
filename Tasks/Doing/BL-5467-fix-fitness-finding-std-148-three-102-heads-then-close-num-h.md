---
id: BL-5467
title: Fix fitness finding STD-148: Three 102 heads then close: %{num_headers} is 0; curl reports 1
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5467 — Fix fitness finding STD-148: Three 102 heads then close: %{num_headers} is 0; curl reports 1

## Goal

STD-148 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Three 102 heads then close: %{num_headers} is 0; curl reports 1. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs, Windows and Linux)
- **Protocol/area:** HTTP/1.1 interim responses, write-out (neighbour of STD-109)
- **Repro:** a TCP server on 127.0.0.1:43001 that sends exactly `HTTP/1.1 102 Processing\r\n\r\nHTTP/1.1 102 Processing\r\nA: b\r\n\r\nHTTP/1.1 102 Processing\r\n\r\n` and closes. `curl -sS -i http://127.0.0.1:43001/c102x3 -w "[%{http_code} %{response_code} %{size_header} %{num_headers}]"`
- **Expected (REF):** `[102 102 87 1]`, `curl: (52) Empty reply from server`.
- **Actual (PORT):** `[102 102 87 0]`.
- **Likely location:** `HttpProtocolHandler.cs` lone-1xx `num_headers` count takes the last head's count; curl's comes from the non-empty middle head.
- **Acceptance check:** the command prints `[102 102 87 1]`.

## Acceptance criteria

- [ ] the command prints `[102 102 87 1]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-148 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 2561fa9a 69116afe 3d836d5e.
