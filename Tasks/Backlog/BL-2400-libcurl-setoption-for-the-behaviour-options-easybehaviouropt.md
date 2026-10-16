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
completed:
---
# BL-2400 — Libcurl: SetOption for the behaviour options (EasyBehaviourOptions)

## Goal

Every `CURLOPT_*` of `EasyBehaviourOptions` (`VERBOSE`, `HEADER`, `NOPROGRESS`, `NOSIGNAL`, `WILDCARDMATCH`, `ERRORBUFFER`, `STDERR`, `FAILONERROR`, `KEEP_SENDING_ON_ERROR`, `PRIVATE`, `SHARE`, `QUICK_EXIT`, `NEW_FILE_PERMS`, `NEW_DIRECTORY_PERMS`) sets its field through `SetOption` and is in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per option sets it and reads the field back from the handle's `EasyOptions`; a wrong value type returns `BadFunctionArgument`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
