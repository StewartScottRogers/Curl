---
id: BL-6022
title: Fix fitness finding STD-225: a malformed --resolve in a later --next group is ignored when that group reuses a kept-alive connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6022 — Fix fitness finding STD-225: a malformed --resolve in a later --next group is ignored when that group reuses a kept-alive connection

## Goal

STD-225 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: a malformed --resolve in a later --next group is ignored when that group reuses a kept-alive connection. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (serial and `-Z`, 3 variants) · **Area:** `--resolve` / `--next`, Windows build.
- **Mock server:** HTTP on `127.0.0.1:24000` with `/hello` → `hello` and `/json` → `{"a":1}`, keep-alive.
- **Repro:** `curl -sS http://127.0.0.1:24000/hello --next --resolve bad http://127.0.0.1:24000/json -w "[%{exitcode}]\n"`
- **Expected (REF):** `hello`, then stderr `curl: (49) Could not parse CURLOPT_RESOLVE entry 'bad'`, stdout `[49]`, exit 49.
- **Actual (.NET):** `hello{"a":1}[0]`, exit 0. With a different host:port in the second group, or a single group, the port correctly exits 49.
- **Likely location:** `Curl.Console/CurlCommandRunner.cs`, `MalformedResolveFailure` (~line 3780), skipped on the connection-reuse path.
- **Acceptance check:** the repro prints `[49]` with REF's stderr line and exits 49.

## Acceptance criteria

- [ ] the repro prints `[49]` with REF's stderr line and exits 49.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 91d27397 69116afe 3d836d5e.
