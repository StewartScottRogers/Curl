---
id: BL-3059
title: Align the Hosts' documentation: glossary terms and README cross-links
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-3030, BL-3031, BL-3032, BL-3033, BL-3034, BL-3035, BL-3036, BL-3037, BL-3038, BL-3039, BL-3040, BL-3041, BL-3042, BL-3043, BL-3044, BL-3045, BL-3046, BL-3047, BL-3048, BL-3049, BL-3050, BL-3051, BL-3052, BL-3053, BL-3054, BL-3055, BL-3056, BL-3057, BL-3058]
touches: [Curl.Api/README.md, Curl.Api.UnitLibrary/README.md, Curl.Console/README.md, Libcurl/README.md, Curl.Mcp/README.md, Curl.Mcp.UnitLibrary/README.md, Documentation/Wiki/Glossary.md]
model: sonnet
requirement: none
created: 2026-10-22
completed:
---
# BL-3059 — Align the Hosts' documentation: glossary terms and README cross-links

## Goal

the six Host READMEs (Curl.Api, Curl.Api.UnitLibrary, Curl.Console, Libcurl, Curl.Mcp, Curl.Mcp.UnitLibrary) and Documentation/Wiki/Glossary.md

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." The comment tasks for this project are listed in `depends-on`; write the README from the code as it stands after them. Every statement must be true of the code now, and intent is written as intent (root CLAUDE.md, "Say what it does, do what it says"). Use Documentation/Wiki/Glossary.md terms and read the project's CLAUDE.md. Cross-links to the other host READMEs are added by the final alignment task, so do not chase them here beyond a plain relative link where natural. Tests: Curl.Api.UnitTests, Curl.Mcp.UnitTests.

## Acceptance criteria

- [ ] The six Host READMEs use the same Glossary.md term for the same concept, and each links to the others it relates to (Curl.Api to Curl.Api.UnitLibrary and Libcurl, Curl.Mcp to Curl.Mcp.UnitLibrary and Curl.Console, and so on) with links that resolve.
- [ ] Any Glossary.md entry the Hosts need and lack is added, and no README or doc comment in the Hosts contradicts a glossary definition.
- [ ] If a test reads or checks the README, it still passes; any code sample in it is exercised by that test where the project already does so.
- [ ] No source file other than the README changes, and `dotnet build` of the project is clean with warnings as errors and the fast tests of Curl.Api.UnitTests, Curl.Mcp.UnitTests pass (`dotnet test Curl.Api.UnitTests, Curl.Mcp.UnitTests --filter "TestCategory!=Integration"`).

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 70a5c288 b6354e0a 0e267b12 1f7e88de.
