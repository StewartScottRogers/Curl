---
id: BL-6025
title: Fix fitness finding STD-228: -Z with a missing -F file in a later --next group reports the failure once; curl reports it for every transfer in the batch
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ParallelRun.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6025 — Fix fitness finding STD-228: -Z with a missing -F file in a later --next group reports the failure once; curl reports it for every transfer in the batch

## Goal

STD-228 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: -Z with a missing -F file in a later --next group reports the failure once; curl reports it for every transfer in the batch. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs, 3 variants) · **Area:** `-Z` / `-F`, Windows build (next to STD-123's fix).
- **Mock server:** HTTP on `127.0.0.1:24000` with `/hello` and `/echo/`.
- **Repro:** `curl -sS -Z -w "[%{urlnum} %{url} %{exitcode}]\n" http://127.0.0.1:24000/hello --next -w "[%{urlnum} %{url} %{exitcode}]\n" -F f=@nofile.txt http://127.0.0.1:24000/echo/`
- **Expected (REF):** two `curl: (26) Failed to open/read local data from file/application` lines; stdout `[0 http://127.0.0.1:24000/hello 26]` and `[1 http://127.0.0.1:24000/echo/ 26]`; exit 26.
- **Actual (.NET):** one `(26)` line and only `[0 http://127.0.0.1:24000/hello 26]`; exit 26.
- **Likely location:** `Curl.Console/ParallelRun.cs`, `AbandonFirstBatchAsync`.
- **Acceptance check:** the repro's stdout and stderr equal REF's.

## Acceptance criteria

- [ ] the repro's stdout and stderr equal REF's.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: a1654503 69116afe 3d836d5e.
