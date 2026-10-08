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
completed:
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

- [ ] The published `Curl.Console` native binary is at least 15% smaller than 17.0 MB. Notes record the before and after sizes and each setting tried.
- [ ] No hot path regresses by more than 5%: the time of the `Curl.Cryptography.UnitTests` X25519 and RSA tests, and an HTTPS GET with Curl's `--trace-time` against a loopback TLS server, measured before and after. Notes record the numbers.
- [ ] Curl's error output, `--version`, `-V` and `--help` text are unchanged by the settings (stack traces are never printed by curl-compatible paths). The fast tests pass.
- [ ] An ADR records the chosen settings and the ones rejected, with the measurements.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-08: Created.
