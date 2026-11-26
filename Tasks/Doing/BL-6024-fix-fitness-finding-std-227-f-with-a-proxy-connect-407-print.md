---
id: BL-6024
title: Fix fitness finding STD-227: -f with a proxy CONNECT 407 prints a different error text
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-26
completed:
---
# BL-6024 — Fix fitness finding STD-227: -f with a proxy CONNECT 407 prints a different error text

## Goal

STD-227 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: -f with a proxy CONNECT 407 prints a different error text. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed · **Area:** HTTP proxy / `-f`, Windows build.
- **Mock servers:** an HTTP proxy on `127.0.0.1:24051` that answers every CONNECT with 407; any HTTPS target (e.g. `127.0.0.1:24443`).
- **Repro:** `curl -sSk -f --proxy-user a:b -x http://127.0.0.1:24051 https://127.0.0.1:24443/hello -w "[%{exitcode}]\n"`
- **Expected (REF):** `curl: (22) HTTP response code said error`, `[22]`.
- **Actual (.NET):** `curl: (22) The requested URL returned error: 407`, `[22]`.
- **Acceptance check:** stderr equals REF's line.

## Acceptance criteria

- [ ] stderr equals REF's line.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 47fc27c5 69116afe 3d836d5e.
