---
id: BL-2406
title: Libcurl: SetOption for the HTTP options (EasyHttpOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Http, Libcurl.UnitTests/Options/Http]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2406 — Libcurl: SetOption for the HTTP options (EasyHttpOptions)

## Goal

Every `CURLOPT_*` of `EasyHttpOptions` (headers, user agent, referer, encodings, redirects, `POST*` including `COPYPOSTFIELDS`, `HTTPGET`, `HTTP_VERSION`, cookies, alt-svc, HSTS, HTTP/2 stream options) is settable and in the options table; `MIMEPOST` and `HTTPPOST` are left to the MIME tasks.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option; `POSTFIELDS` keeps the caller's buffer by reference and `COPYPOSTFIELDS` copies it, each pinned.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Rows live in `Libcurl/Options/Http`: `EasyOptionSetters.Http.cs` implements `AddHttpSetters` and calls `EasyHttpOptionSetters.AddTo`. That method adds 38 rows, one for each option `EasyHttpOptions` holds apart from `MIMEPOST` and `HTTPPOST`, which still answer `NotBuiltIn` and are pinned by a test. The decisions are in ADR-0565.
- Quality gate: the first version used a lambda per row. `Measure-CodeQuality.ps1` counts each cached lambda as a branch in the method that builds it, so `AddTo` measured complexity 74. Rows are now `new EasyOptionSetter(NamedMethod)`, and `Measure-CodeQuality.ps1 -Library Libcurl` reports 100% line and branch coverage, 0 failing members and a worst CRAP of 10. Other option groups (BL-2400 to BL-2407) should build their rows the same way.
- Trap found: in `cond ? ReadOnlyMemory<byte> : null`, the `null` converts through `byte[]` into an empty memory, not a null `ReadOnlyMemory<byte>?`. Cast it with `(ReadOnlyMemory<byte>?)null`.
- Tests: 44 new tests in `Libcurl.UnitTests/Options/Http/EasyHttpOptionSettersTests.cs`. Libcurl.UnitTests has 117 tests and all pass, and so does the whole fast suite. `PassingUpstreamCases.txt` is unchanged, because Libcurl is not on the conformance path.
- `Libcurl/CLAUDE.md` is still true; no edit was needed. `--ai-help` is untouched, since no command-line option changed.
- Follow-up filed: BL-2461. The engine ignores `HttpGet`, so `HTTPGET` after `POSTFIELDS` still posts.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl SetOption sets every EasyHttpOptions CURLOPT but MIMEPOST/HTTPPOST; POSTFIELDS by reference, COPYPOSTFIELDS copied
