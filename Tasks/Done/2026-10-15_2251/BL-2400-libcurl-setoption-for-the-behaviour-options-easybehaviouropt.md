---
id: BL-2400
title: Libcurl: SetOption for the behaviour options (EasyBehaviourOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Behaviour, Libcurl.UnitTests/Options/Behaviour]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2400 — Libcurl: SetOption for the behaviour options (EasyBehaviourOptions)

## Goal

Every `CURLOPT_*` of `EasyBehaviourOptions` (`VERBOSE`, `HEADER`, `NOPROGRESS`, `NOSIGNAL`, `WILDCARDMATCH`, `ERRORBUFFER`, `STDERR`, `FAILONERROR`, `KEEP_SENDING_ON_ERROR`, `PRIVATE`, `SHARE`, `QUICK_EXIT`, `NEW_FILE_PERMS`, `NEW_DIRECTORY_PERMS`) sets its field through `SetOption` and is in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option sets it and reads the field back from the handle's `EasyOptions`; a wrong value type returns `BadFunctionArgument`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Rows live in `Libcurl/Options/Behaviour/EasyOptionSetters.Behaviour.cs`, the `AddBehaviourSetters` part (ADR-0560). The seam matched the plan: `EasyBehaviourOptions` in `Curl.Api.UnitLibrary`; every option already had its row in the options table from BL-2399, so no table change was needed.
- Choices, following libcurl 8.22.0's `setopt.c`: an on/off option is on for any value but 0; `NEW_FILE_PERMS` and `NEW_DIRECTORY_PERMS` take 0 to 0777 and answer `BadFunctionArgument` outside it; `ERRORBUFFER` takes a `StringBuilder`, `STDERR` a `Stream` and `SHARE` an `IShareHandle` (or null), and any other object answers `BadFunctionArgument`; `PRIVATE` keeps any non-delegate value.
- The first quality run measured `AddBehaviourSetters` at complexity 28 (each cached static lambda counts as a branch), so it adds its rows through four smaller methods; the second run reported 0 failing members, worst CRAP 8.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. SetOption sets every EasyBehaviourOptions field through the options table
