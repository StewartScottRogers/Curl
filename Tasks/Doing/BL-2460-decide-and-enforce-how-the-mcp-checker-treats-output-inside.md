---
id: BL-2460
title: Decide and enforce how the MCP checker treats %output{} inside a -w @file template
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2379]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
## Goal

The MCP `curl` tool never lets a `-w @file` template inside `CURL_MCP_ROOT` write a file outside it through `%output{path}`.

## Context

ADR-0564 decision 7: `McpArgumentChecker` (`Curl.Mcp.UnitLibrary/McpArgumentChecker.cs`, BL-2379) checks the `-w @file` path but, reading no file but a `-K` one, does not look inside the template. An agent can write a template holding `%output{/outside}` into the root with `-o` and then run `-w @template`. Choose one: read the template (as `-K` is read) and run `McpOptionValuePaths`' `%output{}` scan over it, or refuse `-w @file` whose file is not empty of `%output{`. Record the choice in an ADR.

## Acceptance criteria

- [ ] A test in `Curl.Mcp.UnitTests` writes a template holding `%output{<outside>/x}` inside the root and `McpArgumentChecker.Check(["-w", "@<template>", "http://h/"])` is refused naming `-w`.
- [ ] A template inside the root with only `%output{<inside>/x}` or none is allowed; an unreadable template is refused.
- [ ] An ADR records the decision, marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1 -Library Curl.Mcp.UnitLibrary` reports no member outside the four gates.

## Notes

Filed by BL-2379.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
