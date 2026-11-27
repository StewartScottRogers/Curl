---
id: BL-6022
title: Fix fitness finding STD-225: a malformed --resolve in a later --next group is ignored when that group reuses a kept-alive connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] the repro prints `[49]` with REF's stderr line and exits 49.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: only the connectors checked `ResolveOverrides.ParseError`, so a transfer that reused a pooled connection never failed. The runner now checks the entries at each transfer's start (`TryCheckResolveEntriesAndParseUrl`, formerly `TryParseQueriedUrl`), after the `-G` query check and before the URL parse, proxy choice and connect. Direct pipeline: a two-method change, so the `/feature` stages' subagents were not worth their cost.
- Measured curl 8.22.0 Schannel with `Record-CurlExchange.ps1` (serial, `-Z`, `file://`, single group, `-v -b nofile`): exit 49, `-w` `%{num_connects}` 0 and `%{conn_id}` -1, `-D` file created, no `-o` file, no progress meter, the cookie-file warning still before the entry line. `%{conn_id}` printed 0 or the next number in Curl, so `UsedAConnection` now excludes this refusal (`IsMalformedResolveRejection`).
- No ADR: this matches curl, with no design choice.
- Added `Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs` to `touches` for the test; no task in Doing names it.
- Left to BL-6040: under `-Z` curl writes the failed transfer's `-w` before the running transfer's body; Curl writes it after.
- Verified: `dotnet build` clean; fast tests 58420 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Console` 0 failing members; repro run on the built `curl.exe` prints `hello[49 0 -1]`, curl's stderr line, exit 49.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 91d27397 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. A malformed --resolve now exits 49 at each transfer's start, a reused connection included, with conn_id -1
