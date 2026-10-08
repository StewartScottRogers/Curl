---
id: BL-1749
title: Shrink Curl.Console's native binary so each launch costs less under on-access scanning (AF-0048)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Directory.Build.props]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1749 — Shrink Curl.Console's native binary so each launch costs less under on-access scanning (AF-0048)

## Goal

The published native `curl.exe` from `dotnet publish Curl.Console` is materially smaller, so a cold launch under Windows Defender's on-access scan costs less. This is the remaining part of AF-0048, after BL-1675 made the transfer itself as fast as curl's.

## Context

- BL-1675 (commit a7c6bb900) made `-o` writes synchronous. The large-get scenario went from 1.67x to 1.40x curl's median. A harness with no gap between runs measures 1.04x, so the transfer now matches curl.
- What remains, measured in BL-1675: about 40 ms before `Main` runs, on launches spaced more than about 5 seconds apart. It costs the same with an early `return 0` in `Main`, and an AOT hello-world padded to 17 MB reproduces it, so the cost tracks binary size. The likeliest cause is Defender scanning each launch. `Audit/Tools/Measure-Performance.ps1` spaces large-get runs about 12 seconds apart, so every Curl launch pays it, while curl's small `curl.exe` barely does.
- Size levers measured in BL-1675 on the 17.0 MB binary:
  - `OptimizationPreference=Size` gives 14.6 MB.
  - `StackTraceSupport=false` gives 16.1 MB.
  - Both are build-wide trade-offs: measure the crypto-heavy paths (X25519, the RSA and brainpool work from BL-1645 and BL-1711) and the five other performance scenarios before choosing.
- Other levers to measure: trimming unused BCL surface (`UseSystemResourceKeys`, `InvariantGlobalization` where curl's behaviour allows), `IlcFoldIdenticalMethodBodies`, and dropping reflection-reachable metadata.
- Decide by measurement, and record the choice and why in an ADR "Decided by Claude under Stewart's delegation". No package, and no change to `CodeMetricsConfig.txt`.

## Acceptance criteria

- [x] The published `Curl.Console` native binary is at least 15% smaller than 17.0 MB. Notes record the before and after sizes and each setting tried.
- [x] No hot path regresses by more than 5%: the time of the `Curl.Cryptography.UnitTests` X25519 and RSA tests, and an HTTPS GET with Curl's `--trace-time` against a loopback TLS server, measured before and after. Notes record the numbers.
- [x] Curl's error output, `--version`, `-V` and `--help` text are unchanged by the settings (stack traces are never printed by curl-compatible paths). The fast tests pass.
- [x] An ADR records the chosen settings and the ones rejected, with the measurements.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Decision: `OptimizationPreference=Size` + `StackTraceSupport=false` in `Curl.Console.csproj` (ADR-0434). They go in the csproj, not `Directory.Build.props`, because only Curl.Console publishes native code. `Directory.Build.props` is unchanged.
- Sizes (win-x64, `dotnet publish Curl.Console -c Release`):
  - before: 17,853,440 bytes (17.0 MiB)
  - `Size`: 15,348,736
  - `StackTraceSupport=false` + `IlcFoldIdenticalMethodBodies`: 16,869,888
  - `Size` + `StackTraceSupport=false`: 14,384,640 (13.7 MiB, -19.4%)
  - that pair + `IlcFoldIdenticalMethodBodies`: 14,384,640 (no gain, so not set)
- Rejected `UseSystemResourceKeys`: it could change BCL/OS message text that reaches curl's error output. `InvariantGlobalization` was already on.
- Hot paths: ILC settings do not affect the JIT-run `Curl.Cryptography.UnitTests`, so their test times cannot move. The same code was measured in native AOT instead, with a throwaway file-based app (outside the repo, `#:project` on Curl.Cryptography.UnitLibrary) published with and without the settings. Best of 18 interleaved runs, on a machine shared with other lanes:
  - X25519 x2000: 135 -> 139 ms (+3.0%)
  - Ed25519 sign+verify x1000: 686 -> 692 ms (+0.9%)
  - RSA-2048 CRT private operation x200: 1338 -> 1347 ms (+0.7%)
  - Brainpool runs on the same big-integer arithmetic as RSA.
- HTTPS GET against `Record-CurlExchange.ps1 -Tls`, median of 9 interleaved runs:
  - `%{time_appconnect}`: 31.6 -> 32.5 ms (+3.1%)
  - `%{time_total}`: 42.5 -> 43.5 ms (+2.4%)
  - Measured with `-w` timings rather than `--trace-time` stamps: they cover the same span and need no parsing.
  - The harness's whole-process wall time (launched from PowerShell, Defender included) is too noisy to compare: medians 83 vs 90 ms, spread 77-136 ms.
- Output: `--version`, `-V`, `--help all`, `--ai-help all`, `--bogus`, refused http and https connects, and an unopenable `file://` give the same bytes before and after. Only the measured ms in the connect-error text differ.
- The spaced cold-launch Defender cost was not re-measured here. That needs the audit office's performance tool, which lanes may not run, so AF-0048's next re-audit measures it.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. curl.exe 19.4% smaller (17.0 to 13.7 MiB) via OptimizationPreference=Size and StackTraceSupport=false; crypto and TLS within 5%; output unchanged; ADR-0434
