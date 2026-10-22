---
id: BL-1436
title: Parse curl's named URL globs and resolve #<name> in an output name
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-04
---
# BL-1436 — Parse curl's named URL globs and resolve #<name> in an output name

## Goal

`Curl.Core.UnitLibrary`'s URL glob accepts curl 8.21.0's named globs - `{<name>a,b}` and `[<name>1-3]` - expands them like unnamed ones, refuses a name used twice with `Duplicate glob name` (exit 3), and lets an `-o` name refer to one with `#<name>`, reporting an unknown name as curl's `no glob exists with this name` (exit 43) error.

## Context

- Upstream (tag `curl-8_21_0`), `src/tool_urlglob.c`:
  - `glob_parse` lines 506-540: after `{` or `[`, an optional `<name>` (at most `MAX_GLOBNAME_LEN` 64 bytes, line 443) names the glob. A broken name syntax (no closing `>`, too long) is not an error: the text is parsed as before, as literal set or range content. A name already used fails with `globerror(glob, "Duplicate glob name", pattern - ipattern, CURLE_URL_MALFORMAT)` - the position is the offset just past the `>`.
  - `glob_match_url` lines 738-761: in the output name, `#<name>` is replaced by the named glob's current value, looked up in the URL glob and then in the `-T` upload glob; a well-formed `#<name>` naming no glob fails with `globerror(glob, "no glob exists with this name", filename - ifilename, CURLE_BAD_FUNCTION_ARGUMENT)` (exit 43). A malformed `#<...` is copied as written. Numbered `#N` references still count every glob, named or not (`globindex`).
- Upstream tests, vendored in `Curl.Conformance.UnitTests/UpstreamTestData/`: test2408 (named `{}` globs), test2409 (named `[]` globs), test2410 (`https://dummy.example/{<test>A,B}{<test>C,D}` gives `curl: (3) Duplicate glob name in position 40:` then the URL and a caret line) and test2411 (`-o "somewhere/#<foo>"` gives `curl: (43) no glob exists with this name in position 16:` then the file name and a caret). Real curl 8.21.0 printed exactly those on 2026-10-04; Curl resolves `dummy.example` four times instead.
- Curl today: `Curl.Core.UnitLibrary/Globbing/UrlGlobParser.cs` has no name syntax; `UrlGlobMatch.SubstituteGlobValues` handles only `#N`. `UrlGlobError` already carries curl's message and position for the URL errors `Curl.Console` prints.
- Scope: this task gives Core the parsing, the substitution and an error-returning substitution API (e.g. a `TryResolveOutputFileName` that returns a `UrlGlobError`) for the unknown-name case. Wiring that error into `Curl.Console`'s `-o` handling and looking names up in the `-T` upload glob (`Curl.Cli.UnitLibrary/UploadFileGlob.cs`) are not part of it; say in Notes what the follow-up needs.

## Acceptance criteria

- [x] Tests in `Curl.Core.UnitTests` pin that `http://h/{<a>x,y}[<b>1-2]` expands to the same four URLs as `http://h/{x,y}[1-2]`, that `#<a>-#<b>` and `#1-#2` both resolve to `x-1` for the first match, and that `{<bad x,y}` and a 65-byte name parse as before (no name).
- [x] A test pins `https://dummy.example/{<test>A,B}{<test>C,D}` failing with `Duplicate glob name`, `CurlExitCode.UrlMalformat` and position 40.
- [x] A test pins `#<foo>` in an output name with no glob named `foo` giving `no glob exists with this name`, `CurlExitCode.BadFunctionArgument` and position 16 for `somewhere/#<foo>`, and `#<foo` (no `>`) copied as written.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean; `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

- Plan: a shared `UrlGlobName.Read` reads `<name>` (`<`, at most 64 characters, first `>`) for both the parser and `#<name>`; `UrlGlobPiece.Name` carries it; `UrlGlobMatch` keeps the names beside `GlobValues`.
- Decided (sensible default, no upstream source on hand): any character but `>` may be in a name, and the empty name `<>` is a name. Upstream test data only exercises letters; revisit if a conformance diff shows otherwise.
- Decided: `Duplicate glob name`'s position is the 0-based offset just past `>` (`pattern - ipattern`, as the task says), not the parser's other errors' closed-set-adjusted column; both give 40 for test2410.
- `SubstituteGlobValues` and `ResolveOutputFileName` keep returning a string and leave an unknown `#<name>` as written, so Curl.Console is unchanged; the new `TryResolveOutputFileName` returns curl's exit 43 failure.
- Follow-up filed: BL-1443 wires `TryResolveOutputFileName` into Curl.Console's `-o` (`UrlTransfer.cs:62`) and looks names up in the `-T` upload glob (`Curl.Cli.UnitLibrary/UploadFileGlob.cs`).
- Measure-CodeQuality -Library Curl.Core.UnitLibrary: 100% line, 100% branch, 0 failing members (after extracting `ReadGlobName`/`ReadNamedGlob` from `ReadNextPiece`, which first measured at complexity 12). Curl.Core.UnitTests: 1413 passed, 6 skipped.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. Core URL globs accept {<name>..}/[<name>..], refuse a duplicate name (exit 3), and resolve #<name> in -o names with curl's exit 43 for an unknown one
