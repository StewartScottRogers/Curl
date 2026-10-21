---
id: BL-1832
title: Leave URLs read from --url @file unglobbed, as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1832 — Leave URLs read from --url @file unglobbed, as curl 8.21.0 does

## Goal

A URL read from `--url @file` or `--url @-` is taken as written, its `{a,b}` and `[1-3]` not expanded, as curl 8.21.0 does.

## Context

- curl 8.21.0's `parse_url` (src/tool_getparam.c) calls `add_url(config, line, TRUE)`, which sets both `useremote` and `noglob` on each URL it reads from the file.
- BL-1804 made `--url @file` add each line as a URL with `UrlOutput.UsesRemoteName`; globbing stayed per option group (`CommandLineOptions.GlobOff`), so a file URL holding `{` or `[` is still expanded.
- Start at `Curl.Cli.UnitLibrary/UrlOutput.cs` (add a per-URL "unglobbed" flag) and where `Curl.Console` chooses `UrlGlob.Unglobbed` over `UrlGlob.TryParse`.

## Acceptance criteria

- [x] `--url @urls` with a line `http://h/{a,b}` requests `/{a,b}` once, unexpanded; a positional `http://h/{a,b}` beside it is still expanded.
- [x] A test in `Curl.Cli.UnitTests` and one in `Curl.Console.UnitTests` pin it.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Added `UrlOutput.IsUnglobbed`, set by `CommandLineOptions.AddUrl(..., readFromUrlFile: true)` (the parameter was `usesRemoteName`; one flag now carries both of curl's `useremote` and `noglob`). `CurlCommandRunner.TryParseGlob` takes such a URL as `UrlGlob.Unglobbed`.
- Behaviour taken from curl 8.21.0's source (`add_url(config, line, TRUE)` in `parse_url`) as the Context cites; not re-measured.
- Delivered directly rather than through the full feature stages: a three-line change with two pinning tests (`Parse_UrlAtFileBesidePositionalUrl_MarksOnlyTheFileUrlUnglobbed`, `RunAsync_UrlAtFileHoldingAGlob_RequestsItOnceUnexpandedBesideAnExpandedPositionalGlob`). Both branches of the new `||` are covered by existing `-g` tests and the new ones.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. URLs read from --url @file are requested as written, never globbed
