---
id: BL-5814
title: Fix fitness finding STD-145: after a TLS verification failure, or a SOCKS handshake that times out, %{num_connects} is 0 and (for SOCKS) %{time_namelookup} is 0 (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5814 — Fix fitness finding STD-145: after a TLS verification failure, or a SOCKS handshake that times out, %{num_connects} is 0 and (for SOCKS) %{time_namelookup} is 0 (STILL OPEN, partly fixed)

## Goal

STD-145 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: after a TLS verification failure, or a SOCKS handshake that times out, %{num_connects} is 0 and (for SOCKS) %{time_namelookup} is 0 (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs each)
- **Protocol/area:** write-out timings, Windows. The SOCKS-RST and refused-CONNECT cases are fixed.
- **Repro:** an HTTPS server with a self-signed certificate on 127.0.0.1:23443; a SOCKS5 server on 127.0.0.1:23015 that accepts the greeting, reads the CONNECT request and never answers; an HTTP server on 23000. `F='%{time_namelookup} %{time_connect} %{num_connects} %{exitcode}\n'`
  1. `curl -s -o NUL https://127.0.0.1:23443/hello -w "$F"`
  2. `curl -s -o NUL -m 2 -x socks5://127.0.0.1:23015 http://127.0.0.1:23000/hello -w "$F"`
  3. `curl -s -o NUL -m 1 -x socks4://127.0.0.1:23015 http://127.0.0.1:23000/hello -w "$F"`
- **Expected (REF):** (1) `0.000053 0.000711 1 60`; (2) `0.000859 0.000000 1 28`; (3) `0.001048 0.000000 1 28`.
- **Actual (.NET):** (1) `0.003064 0.004547 0 60`; (2) `0.000000 0.000000 0 28`; (3) `0.000000 0.000000 0 28`.
- **Likely location:** `Curl.Networking.UnitLibrary/TcpConnector.cs` (failure after the TCP connect; timeout during the SOCKS handshake) and the engine's timing stamps; the DNS stamp and connect count are dropped on these paths.
- **Acceptance check:** (1) prints num_connects `1`; (2) and (3) print a non-zero namelookup and num_connects `1`.

## Acceptance criteria

- [ ] (1) prints num_connects `1`; (2) and (3) print a non-zero namelookup and num_connects `1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.