---
id: BL-1847
title: Close GF-0018 rest: -F several files in one @a,b and quoted file names with , ; "
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1847 — Close GF-0018 rest: -F several files in one @a,b and quoted file names with , ; "

## Goal

Curl sends what curl 8.21.0 sends for upstream tests 1133 and 1315, the two items of gap
finding GF-0018 that BL-1811 left, so a later gap analysis measures `behaviour:test1133` and
`behaviour:test1315` as `match`.

## Context

- Split from BL-1811, which closed `behaviour:test277` and `behaviour:test669` (a `-H`
  Content-Type merged with the form's boundary, in `Curl.Protocol.Http.UnitLibrary`'s
  `HttpRequestHeadFormatter`). These two need the `-F` parser (`Curl.Cli.UnitLibrary`), the
  mapping into `MultipartFormPart` (`Curl.Console/MultipartFormPartMapping.cs`) and maybe
  `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs`, outside BL-1811's touches.
- test1315, `-F 'file=@a,b;type=magic/content,c'`: request differs at byte 390. curl's
  `tool_formparse.c` makes `@a,b,c` one `multipart/mixed` subpart of the `file` form-data
  part, one file part per name, each with its own `;type=`; the inner parts carry
  `Content-Disposition: attachment; filename="..."`.
- test1133, a quoted file name holding `,`, `;` and `"`: request differs at byte 634.
  curl reads a quoted `filename="..."` (backslash escapes) and writes it into
  Content-Disposition with `"` and `\` escaped as curl 8.21.0 does (measure first).
- Reproduce: the gap office's `Measure-UpstreamCases.cs` with cases `1133,1315` (an
  interactive session; lanes may not read `Gap/`), or `Record-CurlExchange.ps1` against real
  curl with the same command lines.

## Acceptance criteria

- [ ] `behaviour:test1315`: Curl's request for `-F 'file=@a,b;type=magic/content,c'` is byte for byte curl 8.21.0's.
- [ ] `behaviour:test1133`: Curl's request for test1133's quoted file name is byte for byte curl 8.21.0's.
- [ ] Unit tests pin both requests; `dotnet build` clean and the fast tests green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
