---
id: BL-1443
title: Wire named URL globs into Curl.Console's -o and the -T upload glob
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1436]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1443 — Wire named URL globs into Curl.Console's -o and the -T upload glob

## Goal

`curl -o "somewhere/#<foo>" "http://h/{<a>x}"` fails as curl 8.21.0 does (test2411: `curl: (43) no glob exists with this name in position 16:`, the file name and a caret), `#<name>` in an `-o` name also finds a glob named in the `-T` upload pattern, and upstream tests 2408-2411 pass through Curl.Console.

## Context

- BL-1436 gave Curl.Core.UnitLibrary named globs: `UrlGlobParser` reads `{<name>..}`/`[<name>..]` and `UrlGlobMatch.TryResolveOutputFileName` returns the exit 43 `TransferResult` for an unknown `#<name>`. `SubstituteGlobValues`/`ResolveOutputFileName` leave an unknown name as written.
- `Curl.Console/UrlTransfer.cs:62` still calls `ResolveOutputFileName`; switch it to `TryResolveOutputFileName` and report the failure as curl does (exit 43, before the transfer).
- Upstream `glob_match_url` (tool_urlglob.c, curl-8_21_0, lines 738-761) looks a name up in the URL glob, then in the `-T` upload glob; `Curl.Cli.UnitLibrary/UploadFileGlob.cs` has no names yet.
- Upstream tests 2408-2411 are vendored in `Curl.Conformance.UnitTests/UpstreamTestData/`.

## Acceptance criteria

- [ ] A Curl.Console test pins `-o "somewhere/#<foo>"` giving exit 43 and curl's three-line message.
- [ ] A test pins `#<name>` resolving to a glob named in the `-T` pattern.
- [ ] `dotnet build` clean; fast tests green; Measure-CodeQuality reports no failing member for the libraries changed.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
