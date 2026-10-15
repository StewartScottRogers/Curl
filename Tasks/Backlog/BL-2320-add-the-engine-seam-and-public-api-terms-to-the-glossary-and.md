---
id: BL-2320
title: Add the engine, seam and public API terms to the glossary and the product overview
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2318, BL-2313, BL-2314, BL-2315]
touches: [Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-10-15
completed:
---
# BL-2320 — Add the engine, seam and public API terms to the glossary and the product overview

## Goal

`Documentation/Wiki/Glossary.md`, `Documentation/Product/Product-Overview.md` and `Documentation/Product/Requirements.md` name the engine, the seam and the two public APIs with one name each, as the code has them after phase 2.

## Context

ADR-0506, plan phase 2 step 6; `align-and-document` work. Terms: transfer engine (`Curl.Engine.UnitLibrary`), seam / engine contracts (`Curl.Api.EngineContracts`), easy transfer (`IEasyTransfer`), easy option group (`Easy<Group>Options`), `EasyOptions`, `TransferInfo`, `CurlCode` versus `CurlExitCode` (ADR-0506, "Names": two concepts, equal numbers), `Libcurl` (the libcurl-faithful API, renamed from `Curl.libcurl` by Stewart on 2026-10-15), `Curl.Api` (the .NET API), `EasyOptionsFromCommandLine` (the tool's `config2setopts`). Requirements: add requirement entries for the two public APIs as intent (status: planned), pointing at the plan, so later tasks can name them in `requirement:`.

## Acceptance criteria

- [ ] `Documentation/Wiki/Glossary.md` has an entry for each term in Context, each naming the type or project it is.
- [ ] `Documentation/Product/Product-Overview.md` describes the layering (tool, `Libcurl`, `Curl.Api`, seam, engine) with a link to the plan.
- [ ] `Documentation/Product/Requirements.md` has requirement IDs for `Libcurl` and `Curl.Api` marked planned.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-15: Created.
