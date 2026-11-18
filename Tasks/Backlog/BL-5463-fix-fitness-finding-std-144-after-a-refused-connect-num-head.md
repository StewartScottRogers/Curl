---
id: BL-5463
title: Fix fitness finding STD-144: After a refused CONNECT, %{num_headers} and %{size_header} are 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5463 — Fix fitness finding STD-144: After a refused CONNECT, %{num_headers} and %{size_header} are 0

## Goal

STD-144 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: After a refused CONNECT, %{num_headers} and %{size_header} are 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTP proxy CONNECT, write-out
- **Repro:** a TCP proxy on 127.0.0.1:43052 answering any request with `HTTP/1.1 403 Forbidden\r\nContent-Length: 4\r\nX-Proxy: yes\r\n\r\nnope`. `curl -sSk -x http://127.0.0.1:43052 https://127.0.0.1:43443/hello -w "[%{exitcode} %{num_headers} %{size_header}]\n"`
- **Expected (REF):** `[7 2 59]`.
- **Actual (PORT):** `[7 0 0]`.
- **Likely location:** `Curl.Networking.UnitLibrary/TcpConnector.cs` CONNECT-refused path (~line 2150): the proxy reply's header lines and bytes are not added to the transfer's header counters.
- **Acceptance check:** the command prints `[7 2 59]`.

## Acceptance criteria

- [ ] the command prints `[7 2 59]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-144 (Low).