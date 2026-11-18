---
id: BL-5443
title: Fix fitness finding STD-125: -f/--fail with a proxy that refuses CONNECT (403/407) exits 7; curl exits 22
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5443 — Fix fitness finding STD-125: -f/--fail with a proxy that refuses CONNECT (403/407) exits 7; curl exits 22

## Goal

STD-125 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: -f/--fail with a proxy that refuses CONNECT (403/407) exits 7; curl exits 22. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (scripts that test for exit 22 after `--fail` see the wrong code)
- **Confidence:** Confirmed (3 runs Windows, 1 Linux)
- **Protocol/area:** HTTP proxy CONNECT, `--fail`
- **Repro:** a TCP proxy on 127.0.0.1:43052 answering any request with `HTTP/1.1 403 Forbidden\r\nContent-Length: 4\r\nX-Proxy: yes\r\n\r\nnope`; another on 43051 answering `HTTP/1.1 407 Proxy Auth\r\nProxy-Authenticate: Basic realm="p"\r\nContent-Length: 4\r\n\r\nauth`.
  - `curl -sSk -f -x http://127.0.0.1:43052 https://127.0.0.1:43443/hello -w "[%{exitcode} %{http_connect}]\n"`
  - `curl -sS -f -p -x http://127.0.0.1:43052 http://127.0.0.1:43000/hello -w "[%{exitcode}]\n"`
  - `curl -sSk -f -x http://127.0.0.1:43051 https://127.0.0.1:43443/hello -w "[%{exitcode}]\n"`
- **Expected (REF and REF2):** `curl: (22) The requested URL returned error: 403` (or `407`), `[22 403]`, exit 22. Without `-f`, and with `--fail-with-body`, REF gives exit 7 `CONNECT tunnel failed, response 403`.
- **Actual (PORT):** `curl: (7) CONNECT tunnel failed, response 403` (or `407`), `[7 403]`, exit 7, even with `-f`.
- **Likely location:** `Curl.Networking.UnitLibrary/TcpConnector.cs` ~line 2158 (`CONNECT tunnel failed, response {code}` → `CurlExitCode.CouldntConnect`); `--fail` is not applied to a CONNECT status ≥ 400.
- **Acceptance check:** the first command prints `curl: (22) The requested URL returned error: 403` and `[22 403]`, exit 22; without `-f` it still exits 7.

## Acceptance criteria

- [x] the first command prints `curl: (22) The requested URL returned error: 403` and `[22 403]`, exit 22; without `-f` it still exits 7.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Handler fix, not the connector: TcpConnector cannot see --fail, so HttpProtocolHandler.FailedConnectResult maps a refused CONNECT (>=400) under -f to exit 22; --fail-with-body and no -f stay exit 7 (REF behaviour per finding). touches widened to the HTTP handler and its tests; no other Doing task holds them. Tests in Cgr242Tests.cs; quality run clean for Curl.Original.Protocol.Http.UnitLibrary. CI on Linux/macOS: change is platform-neutral.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-125 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 3ee16ced 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. -f maps a refused CONNECT (>=400) to exit 22 in HttpProtocolHandler
