---
id: BL-2365
title: Point Protocol-Sub-Consoles-And-MCP-Servers at EngineComposition.CreateProtocolHandlers
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Protocol-Sub-Consoles-And-MCP-Servers.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2365 — Point Protocol-Sub-Consoles-And-MCP-Servers at EngineComposition.CreateProtocolHandlers

## Goal

The planning document cites where protocols are registered as it is now.

## Context

Line 31 cites Curl.Console/CurlComposition.cs lines 144-161 for the per-scheme LazyProtocolHandler registration; after the engine split (ADR-0506) that list is EngineComposition.CreateProtocolHandlers in Curl.Engine.UnitLibrary. Found by BL-2311.

## Acceptance criteria

- [x] Every file and line reference in the document names where the code is now.

## Notes

Replaced the stale line range with the method name, EngineComposition.CreateProtocolHandlers, since line numbers drift; the other file references (CurlHelpCategories.cs, CurlAiHelpText.cs) exist.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Doc now cites EngineComposition.CreateProtocolHandlers
