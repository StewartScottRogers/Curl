---
id: BL-5466
title: Fix fitness finding STD-147: When a response ends after only 1xx heads, %{header_json} and %header{} show the 1xx header fields; curl shows none
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Engine.UnitLibrary/TransferHeaderStore.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5466 — Fix fitness finding STD-147: When a response ends after only 1xx heads, %{header_json} and %header{} show the 1xx header fields; curl shows none

## Goal

STD-147 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: When a response ends after only 1xx heads, %{header_json} and %header{} show the 1xx header fields; curl shows none. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs, Windows and Linux)
- **Protocol/area:** HTTP/1.1 interim responses, write-out (neighbour of STD-109)
- **Repro:** a TCP server on 127.0.0.1:43001 that reads the request, sends exactly `HTTP/1.1 103 Early\r\nLink: </a>\r\nX-A: 1\r\n\r\n` and closes. `curl -sS -o NUL http://127.0.0.1:43001/c103only -w "%{header_json}|%header{link}|%header{x-a}"`. Also with the response `HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n`.
- **Expected (REF and REF2):** `{` newline `}||`, then `curl: (52) Empty reply from server`, exit 52.
- **Actual (PORT):** `{"link":["</a>"],` newline `"x-a":["1"]` newline `}|</a>|1`, same stderr and exit.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` lone-1xx reporting with `Curl.Engine.UnitLibrary/TransferHeaderStore.cs`: the 1xx lines are stored with normal-header origin instead of 1xx origin.
- **Acceptance check:** the command prints `{` newline `}||`.

## Acceptance criteria

- [ ] the command prints `{` newline `}||`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-147 (Low).