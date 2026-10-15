---
id: BL-2267
title: Write -w %header{name:all:separator} as every value of the header joined by the separator so upstream cases 764 and 765 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2267 — Write -w %header{name:all:separator} as every value of the header joined by the separator so upstream cases 764 and 765 pass

## Goal

`curl -w '%header{this:all:***}\n'` writes every value of the `this` header, in order, joined by `***` (`one***two***three***four`), as curl 8.22.0 does, so upstream cases 764 and 765 pass.

## Context

BL-2264 (2026-10-15) made the conformance harness report `headers-api`, so 764 and 765 now run and are Inconclusive. Measured: 764 writes an empty line where `one***two***three***four` is expected, and 765 (`%header{this:all:-{\}-}`, a `}` escaped with `\` inside the separator) writes `-}` where `one-{}-two-{}-three-{}-four` is expected. curl's `tool_writeout.c` (`writeOutput`, the `%header{` branch) reads `name:all:sep` and joins every value; `\}` in the separator is a literal `}`. `-w %header{...}` lives in `Curl.Output.UnitLibrary` (`TransferWriteOutVariables.cs`).

## Acceptance criteria

- [x] A focused unit test in `Curl.Output.UnitTests` pins `%header{name:all:sep}` joining every value, and `\}` inside the separator.
- [x] 764 and 765 pass (`CURL_UPSTREAM_CASE=764`, Manual category) and are in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `--ai-help`'s `-w` text stays right if it names `%header{}`.
- [x] The fast tests are green.

## Notes

- Case 764 expects the values of every response in the `-L` chain (`one`, `two` from the 301,
  `three`, `four` from the 200), but `TransferReport.ResponseHeaders` held only the last
  response's. Added `TransferReport.EarlierResponseHeaders`, filled by `RedirectFollower`'s
  merged report, so `touches` gained `Curl.Protocol.Abstractions.UnitLibrary`,
  `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`. No other task in Doing on
  `origin/work/dark-factory` named them. Decision in ADR-0501.
- `IWriteOutVariableSource.FindAllHeaderValues` is a default interface method (the first value
  alone), so `Curl.Console.UnitTests`' fake source needs no change.
- `--ai-help` builds its `-w` section from curl's manual (`CurlManualMarkdown`) and names no
  `%header{}` text of its own, so nothing changed there.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary,Curl.Core.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`
  gives 100% line and branch coverage on all three, max complexity 10, 0 failing members.
  Fast tests all green; 764 and 765 pass with `CURL_UPSTREAM_CASE`.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. -w %header{name:all:separator} joins every value from every hop of a -L chain, \} is a literal brace; upstream 764 and 765 pass
