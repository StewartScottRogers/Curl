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
completed: 2026-10-08
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

- [x] `behaviour:test1315`: Curl's request for `-F 'file=@a,b;type=magic/content,c'` is byte for byte curl 8.21.0's.
- [x] `behaviour:test1133`: Curl's request for test1133's quoted file name is byte for byte curl 8.21.0's.
- [x] Unit tests pin both requests; `dotnet build` clean and the fast tests green.

## Notes

- Measured 2026-10-08 with `Record-CurlExchange.ps1`, reference curl 8.21.0 (Schannel) and
  this branch's `Curl.Console`, on upstream test1315's command line
  (`-F name=value -F 'file=@log/test1315.txt,log/test1315.txt;type=magic/content,log/test1315.txt'`)
  and test1133's (five `-F`s: quoted `log/test1133,and;.txt` file names, `filename="faker,and;.txt"`,
  a `@"a",b` group, a JSON text and a quoted text with `\\` and `\"`). With the random
  boundaries replaced, both requests are identical byte for byte, headers and
  Content-Length included. No production change was needed: the `-F` parser, the mapping and
  the builder already do what curl does.
- Cause of the two gap results: both requests contain a nested `multipart/mixed` part with
  its own random boundary, at byte ~390 (test1315) and ~634 (test1133), exactly where the
  measured difference was. The reference cross-check's boundary normaliser (BL-1791, Notes)
  replaces only the boundary named in the request's top-level `Content-Type:` header, so
  the inner boundary still differs between the two runs. The fix is in the gap office's
  `Measure-ReferenceCrossCheck.ps1` (`Set-FixedMultipartBoundary`: replace every
  `boundary=` value found in the request, nested ones too). That path is out of a lane's
  reach - the audit guard refuses even filing the task from a lane - so an interactive
  session should file and do it; GF-0018 then closes on the next gap run.
- Pinned: `Curl.Cli.UnitTests` parses both upstream command lines into the measured part
  tree; `Curl.Core.UnitTests` builds test1315's body with the recorded boundaries and
  compares it byte for byte (Content-Length 824).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Curl already sends curl 8.21.0's requests for upstream test1133 and test1315; both pinned by unit tests; the remaining gap is the cross-check's nested-boundary normalisation (Notes)
