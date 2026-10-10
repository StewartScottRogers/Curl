---
id: BL-1996
title: Close GF-0066: A backslash in a URL path is sent as '/', where curl sends it as written
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1996 — Close GF-0066: A backslash in a URL path is sent as '/', where curl sends it as written

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0066 (A backslash in a URL path is sent as '/', where curl sends it as written), so a later gap analysis measures each of `behaviour:test214` as `match`.

## Context

- Finding: GF-0066, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test214`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test214 (URL path written as backslash-brace, backslash-brace, backslash-slash, then 214) expected 'upstream test214 passes', actual '<verify><protocol> differs at byte 7 (line 1): expected "GET /{}\\/214 HTTP/1.1\r\n", got "GET /{}//214 HTTP/1.1\r\n"'. The glob escapes before { and } are removed, as they should be, but the backslash before '/' must stay. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 214

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's URL globbing (ADR-0032), unescape only the glob characters (a backslash before '[', ']', '{', '}', and ',' inside a set) and keep any other backslash. In Curl.Core.UnitLibrary's URL model, send a backslash in an http path as curl 8.21.0 does: as written, not turned into '/' the WHATWG way. Pin test214's request line.

## Acceptance criteria

- [x] `behaviour:test214`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- The glob side already worked: `UrlGlobParser` drops a backslash only before `{`, `[`, `}`
  or `]`, so `\{\}\/214` became `{}\/214`. Pinned with a `UrlGlobTests` row.
- The `/` came from `CurlUrlParser` in `Curl.Protocol.Abstractions.UnitLibrary`, which turned
  every `\` before `?`/`#` into `/` after `scheme://`. Measured with Record-CurlExchange.ps1
  against curl 8.21.0 (Windows, Schannel): `http://h/a\b` sends `GET /a\b`;
  `/a\..\b`, `/a\\b`, `http:///h/a\b` and `?c\d` keep every backslash; a backslash in
  the authority (`http://h:1\x/`, `http://localhost\x/`, `http://\h/x`, `http:\\h/x`,
  `http:/\h/x`) exits 3. So the conversion now applies to `file:` URLs only (their
  earlier Windows measurements are unchanged), and a backslash elsewhere stays in the path
  or makes the host invalid, which `CurlUrlHost` already rejects.
- Added `Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests` (the parser) and
  `Curl.Protocol.Dict.UnitTests` to `touches`: no task in Doing on
  `origin/work/dark-factory` named them. The DICT test row pinned the old `/`; curl sends
  `dict://h/d:a\b` as `DEFINE ! a\\b`, which Curl's existing word escaping now produces.
- Curl.Console's built binary sends `GET /{}\/214 HTTP/1.1` for test214's URL, as curl does.
- No ADR: this corrects a measurement, not a design choice.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Backslashes in non-file URL paths are sent as typed; test214's request line matches curl 8.21.0
