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
completed: 2026-11-26
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

- [x] the repro's stdout and stderr equal REF's.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 Schannel (2026-11-26, `Record-CurlExchange.ps1`): under `-Z` curl abandons the first batch while
  setting up the failing `-F` group's first URL, so it reports every earlier group's transfer and that one, sends
  nothing, and reports no later URL (variants: one or two URLs before, two URLs in the failing group, a group after
  it, failing first group, `--parallel-max 1` and `2`, `--fail-early`). Curl's stdout, stderr and exit code are
  byte-identical to curl's for the repro and seven variants; the repro reproduced before the change by
  `ParallelRun`'s `reportsOnlyFirst` reporting one transfer for the whole batch.
- Fix kept inside `ParallelRun.cs` because BL-6022 (Doing) holds `CurlCommandRunner.cs`: `ParallelRun` tells option
  groups apart by how many dispatches `CloseAtEnd` has received. The test went into
  `Curl.Console.UnitTests/Cgr175Tests.cs`, beside the existing `-Z -F` missing-file tests.
- No ADR: this matches measured curl, with no behaviour choice to make.
- Left for BL-6043: checking the form at the group's first URL (differs when that URL is in the batch and the group's
  last is not), and `%{xfer_id}` (`0` in curl, `-1` here) for an earlier group's transfer.
- Quality run for `Curl.Console`: 0 failing members. Platform-neutral change and test.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: a1654503 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. -Z with a missing -F file in a later --next group reports every earlier transfer and that group's first, as curl does
