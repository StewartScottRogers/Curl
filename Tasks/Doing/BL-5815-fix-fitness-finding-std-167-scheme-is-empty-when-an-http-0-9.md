---
id: BL-5815
title: Fix fitness finding STD-167: %{scheme} is empty when an HTTP/0.9 reply is rejected (exit 1)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.Scheme.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5815 — Fix fitness finding STD-167: %{scheme} is empty when an HTTP/0.9 reply is rejected (exit 1)

## Goal

STD-167 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: %{scheme} is empty when an HTTP/0.9 reply is rejected (exit 1). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs Windows; also Linux vs REF2)
- **Protocol/area:** HTTP/1.1 write-out (neighbour of the fixed STD-146)
- **Repro:** a raw TCP server on 127.0.0.1:23001 that answers any request with `garbage\r\n\r\n` and closes. `curl -s http://127.0.0.1:23001/nostatus -w "[%{scheme}|%{num_connects}|%{conn_id}|%{exitcode}]\n"`
- **Expected (REF):** `[http|1|0|1]`
- **Actual (.NET):** `[|1|0|1]`
- **Likely location:** `Curl.Engine.UnitLibrary/TransferInfoFromReport.Scheme.cs` line 56: `RequestSchemeOf` returns null for every `UnsupportedProtocol` exit; exclude `ErrorMessage == "Received HTTP/0.9 when not allowed"`, as `IsUnsupportedProtocolRejection` in `CurlCommandRunner.cs` (~line 3866) already does.
- **Acceptance check:** the command prints `[http|1|0|1]`.

## Acceptance criteria

- [ ] the command prints `[http|1|0|1]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: d9dd65df 69116afe 3d836d5e.
