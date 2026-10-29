---
id: BL-4079
title: Separate Curl's extensions from curl's own protocols: Curl.Extension.* projects, the engine's extension seam and the isolation build check (ADR-0999)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Directory.Build.props, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Console/CurlComposition.cs, Curl.Extension.Composition.UnitLibrary, CLAUDE.md]
lane: no
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4079 — Separate Curl's extensions from curl's own protocols: Curl.Extension.* projects, the engine's extension seam and the isolation build check (ADR-0999)

## Goal

Curl's extensions (what it adds beyond upstream curl) have their own projects, a seam into the engine and a build check that keeps them apart from curl's own protocols, so lanes can build extensions in parallel.

## Context

Stewart, 2026-10-29: "restructure the solution so that we have a clean separation between the original protocols and the new extended protocols", approved with "go". ADR-0999 records the design.

## Acceptance criteria

- [x] ADR-0999 records the names, the dependency rules, the seam, the registration list and the drop-in safety rules.
- [x] `Curl.Extension.Composition.UnitLibrary` and `.UnitTests` exist in `/Extensions/Composition/` in `Curl.slnx`; `ExtensionProtocolHandlers.For` gives no factory under `CURL_EXTENSIONS=0`.
- [x] `EngineComposition.CreateProtocolHandlers` takes `extensionHandlers` over `ProtocolHandlerServices`, puts them after curl's handlers, and a clash with a curl scheme fails `ProtocolDispatcher`; `Curl.Console` passes the extensions in both production compositions.
- [x] `VerifyExtensionIsolation` fails the build for each of the three rules (checked with throwaway projects).
- [x] `ExtensionCatalogue.cs` is `merge=union` and in `Tasks/SharedListFiles.txt`; CLAUDE.md, `DarkFactory/LaneRules.md` and the Glossary describe extensions.
- [x] `dotnet build` is clean and the fast tests of the four affected test projects pass.

## Notes

- 2026-10-29: whole solution builds with 0 warnings. Fast tests: Curl.Extension.Composition.UnitTests 8/8, Curl.Protocol.Abstractions.UnitTests 1574 passed, Curl.Engine.UnitTests 2915 passed, Curl.Console.UnitTests 3044 passed, none failed. The check runs before `BeforeBuild` so transitive references (test projects that reference `Curl.Console`) are not counted.
- Left for later tasks: `Curl.Extension.Abstractions` (made by the first extension that needs a hook beyond `IProtocolHandler`), the `--extensions` option and `--ai-help extensions`, and extensions for `Curl.Api` and `Curl.Mcp`, which run through `ManagedCurlEngine`.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9f96eb49 4d5fc885 5b121744 53fcb3ce 79021141 e262aa67.
- 2026-10-29: Doing -> Done. Extension projects, engine seam, isolation build check and docs landed (ADR-0999)
