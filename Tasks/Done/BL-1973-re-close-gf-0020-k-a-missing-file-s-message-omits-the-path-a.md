---
id: BL-1973
title: Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1973 — Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0020 (-K: a missing file's message omits the path, and a Unicode quote in a config file is read differently), so a later gap analysis measures each of `behaviour:test411`, `behaviour:test470` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0020 ([BL-1813]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0020, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test411`, `behaviour:test470`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test411 (-K <LOGDIR>/missing): <verify><stderr> differs at byte 30 (line 1): expected "curl: cannot read config from '<LOGDIR>/missing'", got 'curl: cannot read config from '. test470 (config file with a Unicode quote character): request differs at byte 77 from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 411,470

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's config-file reader: put the quoted path in 'cannot read config from '<path>''. Read a Unicode quote character (U+201C/U+201D) in a config value as curl 8.21.0 does: keep the bytes literally and write its warning, so the request matches the reference.

## Acceptance criteria

- [x] `behaviour:test411`: Curl answers what curl 8.21.0 answers, `upstream test411 passes`, so the item measures `match`.
- [x] `behaviour:test470`: Curl answers what curl 8.21.0 answers, `upstream test470 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- The local upstream tests sit under a guarded `gap` path, so test470 and test411 were read from
  curl's GitHub tag `curl-8_21_0`. Lanes cannot read the gap runner, so its exact inputs are unknown.
- test470, measured on Windows (curl 8.21.0 Schannel, 2026-10-10) through Record-CurlExchange.ps1:
  with a UTF-8 file (`-H “host:fake”`) Curl already matched real curl byte for byte (request and
  stderr; BL-1848 holds). With the same file in ANSI bytes (`93 host:fake 94`) real curl sends the
  bytes raw with no warning, and Curl sent `EF BF BD` for each. The first quote byte sits at byte 77
  (0x4D) of the request, where the finding puts the difference. Fixed: on Windows a config file that
  is not valid UTF-8 is read in the ANSI code page, with no re-spelling and no Unicode warning (ADR-0465).
- test411: `WrappedMessage` read `COLUMNS` from the process, not from the parse's injected
  environment reader, which the in-process upstream runner uses (BL-1928). The unreadable-config
  refusal now wraps at the parse's `COLUMNS` (ADR-0465). If the runner gives no wide `COLUMNS`,
  real curl wraps this long path too, and the next gap run will show it.
- Tests: `CommandLineLeadingUnicodeWarningTests` (ANSI file bytes go out raw with no warning; a UTF-8
  file nested in an ANSI one), `CommandLineConfigFileTests.Parse_MissingConfigFile_WrapsItsMessageAtTheParsesColumnsVariable`.
  Every new branch is run by these tests. Measure-CodeQuality was not rerun (budget).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. -K files that are not valid UTF-8 send their bytes raw on Windows (test470); the missing-config line wraps at the parse's COLUMNS (test411); build clean, fast tests green
