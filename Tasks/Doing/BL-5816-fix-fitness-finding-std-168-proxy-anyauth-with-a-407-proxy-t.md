---
id: BL-5816
title: Fix fitness finding STD-168: --proxy-anyauth with a 407 proxy that closes the connection reports %{http_connect}, %{num_headers} and %{size_header} as 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5816 — Fix fitness finding STD-168: --proxy-anyauth with a 407 proxy that closes the connection reports %{http_connect}, %{num_headers} and %{size_header} as 0

## Goal

STD-168 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --proxy-anyauth with a 407 proxy that closes the connection reports %{http_connect}, %{num_headers} and %{size_header} as 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTP proxy CONNECT write-out (neighbour of the fixed STD-144)
- **Repro:** a proxy on 127.0.0.1:23051 that answers every CONNECT with `HTTP/1.1 407 Proxy Auth` and `Proxy-Authenticate: Basic realm="p"` and then closes; an HTTPS server on 23443. `curl -sSk --proxy-anyauth -U a:b -x http://127.0.0.1:23051 https://127.0.0.1:23443/hello -w "[%{exitcode} %{http_connect} %{num_headers} %{size_header}]\n"`
- **Expected (REF):** `curl: (56) Recv failure: Connection was reset`, then `[56 407 2 83]`.
- **Actual (.NET):** same stderr, then `[56 000 0 0]`. With `--proxy-basic` or no auth option the port matches REF.
- **Likely location:** `Curl.Networking.UnitLibrary/TcpConnector.cs`, the CONNECT auth-negotiation retry path discards the first 407's status and header counters when the retry fails.
- **Acceptance check:** the command prints `[56 407 2 83]`.

## Acceptance criteria

- [ ] the command prints `[56 407 2 83]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 4be73559 69116afe 3d836d5e.
