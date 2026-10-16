---
id: BL-2311
title: Align Curl.Console and Curl.Engine documents with the engine split
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2310]
touches: [Curl.Console/CLAUDE.md, Curl.Engine.UnitLibrary/CLAUDE.md, Documentation/Product/Product-Overview.md, Documentation/Wiki]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2311 — Align Curl.Console and Curl.Engine documents with the engine split

## Goal

Every statement in `Curl.Console/CLAUDE.md`, `Curl.Engine.UnitLibrary/CLAUDE.md`, `Documentation/Product/Product-Overview.md` and `Documentation/Wiki` about where transfer code lives is true after the engine split.

## Context

ADR-0506; plan phase 1 step 5. Each move task kept its own paragraphs right; this is the `align-and-document` pass over the whole split ("say what it does, do what it says"): `Curl.Console/CLAUDE.md` (~46 KB) was written when the console held the engine, so file references, "the runner does X" sentences and composition descriptions need checking against the code. `Documentation/Planning/Protocol-Sub-Consoles-And-MCP-Servers.md` cites `Curl.Console/CurlComposition.cs` line numbers; it is outside this task's `touches`, so if it needs correcting, file a follow-up task rather than editing it here.

## Acceptance criteria

- [x] Every type a sentence in `Curl.Console/CLAUDE.md` names is defined in `Curl.Console`, or the sentence says which project holds it.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` describes every public or internal type in the engine at the level `Curl.Console/CLAUDE.md` did.
- [x] `Documentation/Product/Product-Overview.md`'s architecture section names `Curl.Engine.UnitLibrary` and the tool/engine split.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes (doc comments may have changed).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Checked every type named in Curl.Console/CLAUDE.md against its defining project: engine types now name Curl.Engine (RoutingFtpProtocolHandler, OptionGroupConnectionSettings, ProcessUserId fixed). Engine CLAUDE.md now covers EngineTraceComponents, the one undocumented type. Product-Overview layers, dependency line and project layout updated (layout counts replaced by a rule, since the tree had drifted). Wiki Command-Line-Parsing no longer says Console does not call the parser. Follow-up BL-2365 for the Planning doc outside touches.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Documents aligned with the engine split
