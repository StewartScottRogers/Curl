---
id: BL-1443
title: Wire named URL globs into Curl.Console's -o and the -T upload glob
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1436]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
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

- [x] A Curl.Console test pins `-o "somewhere/#<foo>"` giving exit 43 and curl's three-line message.
- [x] A test pins `#<name>` resolving to a glob named in the `-T` pattern.
- [x] `dotnet build` clean; fast tests green; Measure-CodeQuality reports no failing member for the libraries changed.

## Notes

- Touches widened (2026-10-07): `Curl.Core.UnitLibrary`, because `UrlGlobMatch` keeps its glob names private and the `-T` lookup has to read the upload match's names; `Curl.Conformance.UnitTests`, to list 2408-2411 in `PassingUpstreamCases.txt` once they passed. No task in Doing on `origin/work/dark-factory` named either (BL-1528 touches `Curl.Core.UnitTests` only, which this task leaves alone).
- Core: `UrlGlobMatch.TryResolveOutputFileName(string, UrlGlobMatch? uploadMatch, bool, out, out)` looks a `#<name>` up in the URL's globs, then in the upload match, as upstream `glob_match_url` does; `#N` still counts the URL's globs only (the task's Context names only the name fallback). The old overload delegates with no upload match.
- Cli: `UploadFileGlob.ExpandUploadMatches()` yields the `-T` matches with their glob values; `ExpandUploadFiles` maps them to names.
- Console: `UrlTransfer.TryCreate` resolves the `-o` name and fails with exit 43; `TransferEachMatchAsync` prints it as a glob failure (`curl: (43) ` + the three-line message) and ends the run before that transfer, as a malformed glob does.
- Tests: `UrlTransferTests.TryCreate_NamedReferenceToNoGlob_FailsWithExit43AndNoTransfer`, `TryCreate_NamedReferenceToUploadGlob_TakesTheUploadGlobValueAndFile`; `CurlCommandRunnerUrlExpansionTests.RunAsync_OutputNameReferencesNoGlobWithThatName_Exits43BeforeAnyTransfer`, `RunAsync_OutputNameReferencesAnUploadGlobName_SavesEachUploadUnderItsValue`, `RunAsync_OutputNameReferencesANamedGlob_SavesEachUrlUnderItsValue`. Upstream 2408-2411 pass and are now in `PassingUpstreamCases.txt`.
- Measure-CodeQuality -Library Curl.Core.UnitLibrary,Curl.Cli.UnitLibrary,Curl.Console: 0 failing members.
## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. -o '#<name>' naming no glob exits 43 with curl's message, and a name also resolves from the -T upload glob; upstream 2408-2411 pass
