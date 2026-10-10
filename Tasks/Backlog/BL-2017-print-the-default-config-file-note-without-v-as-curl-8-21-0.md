---
id: BL-2017
title: Print the default config file Note without -v as curl 8.21.0 does for upstream test433 (curlrc found through XDG_CONFIG_HOME)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
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

- [ ] Upstream test433 passes in `UpstreamConformanceTests` and `433` is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The rule that prints the note is pinned in `Curl.Console.UnitTests` or `Curl.Cli.UnitTests` from a measurement of curl 8.21.0, recorded under Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-10: Created.
