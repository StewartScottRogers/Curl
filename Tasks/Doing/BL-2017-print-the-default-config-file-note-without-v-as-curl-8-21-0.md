---
id: BL-2017
title: Print the default config file Note without -v as curl 8.21.0 does for upstream test433 (curlrc found through XDG_CONFIG_HOME)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2017 — Print the default config file Note without -v as curl 8.21.0 does for upstream test433 (curlrc found through XDG_CONFIG_HOME)

## Goal

Upstream test433 passes through Curl: with the default config file found through `XDG_CONFIG_HOME` and no `-v`, Curl writes `Note: Read config file from '<path>'` to standard error as curl 8.21.0 does.

## Context

- Split from BL-1977 (gap finding GF-0047, item `behaviour:test433`). BL-1977 made the case's `<setenv>` reach Curl, so the curlrc is now found and read; the run's first remaining difference is `<verify><stderr>`: expected `Note: Read config file from '%PWD/%LOGDIR/curlrc'`, got nothing.
- `CurlCommandRunner.WriteDefaultConfigFileNoteAsync` writes the note only when `CommandLineParseResult.NotedDefaultConfigFile` is set, which needs `-v` or a `--trace` option (BL-243). test433 has neither: its command is `%HOSTIP:%HTTPPORT/433 --no-progress-meter`, and its curlrc holds `--next`, `header = "a: a"` and `data = "curlrc read"`. Measure real curl 8.21.0 with Record-CurlExchange.ps1 to learn which condition prints the note here (the `--next` in the file, the XDG path, or something else) before changing the rule; keep BL-243's pinned cases right.
- Check it with `dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet"` and the test433 row's verdict.

## Acceptance criteria

- [x] Upstream test433 passes in `UpstreamConformanceTests` and `433` is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] The rule that prints the note is pinned in `Curl.Console.UnitTests` or `Curl.Cli.UnitTests` from a measurement of curl 8.21.0, recorded under Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Measured curl 8.21.0 (Schannel, Windows, 2026-10-10) with Record-CurlExchange.ps1, `XDG_CONFIG_HOME` pointing at a curlrc, `HOME` and `CURL_HOME` unset, command `http://127.0.0.1:47433/433 --no-progress-meter`: curlrc `--next` + `header = "a: a"` + `data = "curlrc read"`, the same without `--next`, `--next` alone, and `header` then `--next` all print **no** note (the first two POST `curlrc read` with `a: a`, exactly test433's `<protocol>`). `--trace-time` alone prints none either. Adding `--include --trace-ascii <file> --trace-config all --trace-time` (what runtests.pl puts before every command) prints `Note: Read config file from '<path>'`. So Curl's rule (BL-243: the note needs `-v` or a trace option) was already right; the note in test433 comes from runtests.pl's trace options, which the harness left out (its old remark cited ADR-0029).
- Decision (no ADR: it matches runtests.pl, it decides no Curl behaviour): `UpstreamCaseRunner` now passes `--trace-ascii %LOGDIR/trace%TESTNUMBER` (`--trace` under `binary-trace`), `--trace-config all` and `--trace-time` after `--include`, as runtests.pl does. The ratchet stayed green (1188 passed, 0 failed) with them.
- Two more harness gaps test433 needed: it has two `<setenv>` parts and the runner read only the first (getpart joins parts of one name), so `COLUMNS=300` never reached the run; and the dialing `CurlComposition.CreateRunner` the conformance tests use passed no `terminalColumns`, so the note wrapped at 79. The runner now reads every `<setenv>` part and that `CreateRunner` resolves `COLUMNS` through its injected environment (no console).
- touches: added `Curl.Conformance.UnitLibrary` and `Curl.Conformance.UnitTests` (the harness and its tests); no other task was in Doing on origin/work/dark-factory.
- Pinned in `CommandLineDefaultConfigFileTests`: `--trace-ascii ... --trace-time` notes the file; `--trace-time` alone and test433's curlrc without `-v` note none.
- The ratchet run also lists about 90 cases as "passes; add to PassingUpstreamCases.txt" (already so before this change); only 433 is added here, since some are Windows-only (HTTPS on Schannel) and listing them needs a Linux and macOS check.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
