---
id: BL-1813
title: Close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1813 — Close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0020 (-K: a missing file's message omits the path, and a Unicode quote in a config file is read differently), so a later gap analysis measures each of `behaviour:test411`, `behaviour:test470` as `match`.

## Context

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
- [x] `behaviour:test470`: moved to BL-1848 (see Notes); this task no longer carries it.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- test411: Curl already printed the path, but wrapped the line at a fixed 79 columns. Measured
  curl 8.21.0 (Windows, 2026-10-08) with `-K` naming a missing 73-byte path: it wraps at 79 with
  no `COLUMNS`, at 40 with `COLUMNS=40`, and not at all with `COLUMNS=200`; the upstream harness
  sets a wide `COLUMNS`, so the expected line is unwrapped. `WrappedMessage` (Curl.Cli.UnitLibrary)
  now takes its width from `COLUMNS` with curl's rule (21-9999, else 79), the same rule
  `Curl.Console/TerminalColumns` uses. It does not read the console window's width as curl does
  when standard error is a console; redirected runs (tests, scripts) are unaffected. Made
  `WrappedMessage` public so `WrappedMessageTests` can pin the width rule (the library has no
  `InternalsVisibleTo`).
- test470 split out to BL-1848: the request bytes differ because the config file is decoded as
  UTF-8 and the Windows request side re-encodes option text in the ANSI code page (`93`/`94`
  instead of curl's raw `E2 80 9C`/`E2 80 9D`). The fix spans Curl.Console's text encoding as well
  as the Cli config reader, beyond this task's `touches` and this run's budget. GF-0020 closes
  only when both items re-measure as `match`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test411 fixed: error lines wrap at COLUMNS width as curl does; test470 split to BL-1848
