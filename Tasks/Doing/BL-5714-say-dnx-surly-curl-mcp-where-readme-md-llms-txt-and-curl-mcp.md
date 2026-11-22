---
id: BL-5714
title: Say dnx Surly.Curl.Mcp where README.md, llms.txt and Curl.Mcp.csproj still say dnx Curl.Mcp
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [README.md, llms.txt, Curl.Mcp/Curl.Mcp.csproj]
requirement: none
created: 2026-11-21
completed:
---
# BL-5714 — Say dnx Surly.Curl.Mcp where README.md, llms.txt and Curl.Mcp.csproj still say dnx Curl.Mcp

## Goal

Every live instruction to run the MCP server names the package it is published as, `dnx Surly.Curl.Mcp`, not the old `dnx Curl.Mcp`.

## Context

Found by the shift-end review (ADR-0710) of 18c1d39a1e...8d41979abc, `pr-review-toolkit:code-reviewer`.
ADR-1333 renamed the NuGet package IDs to the `Surly.` prefix, and `Curl.Mcp/Curl.Mcp.csproj` now packs
`PackageId` `Surly.Curl.Mcp`. `.github/site/mcp/index.html`, `Curl.Mcp/README.md` and
`.github/release/New-ResourcesJson.ps1` were updated to `dnx Surly.Curl.Mcp`, but three places were missed:

- `README.md:133`, the Curl.Mcp row: "Run it with `dnx Curl.Mcp`". That package ID no longer exists, so the command fails.
- `llms.txt:16`: "run it over stdio with `dnx Curl.Mcp --yes --prerelease --add-source <folder>`". The same failure, and an AI agent reading this file will follow it.
- `Curl.Mcp/Curl.Mcp.csproj:11`: the comment says the tool package is what "`dnx Curl.Mcp` runs", and the `PackageId` a few lines below it contradicts that.

This breaks the "say what it does" rule. `Documentation/Planning/Decisions/ADR-0596-*.md` keeps the old name because it is a historical record. Leave it as it is.

## Acceptance criteria

- [ ] `README.md`, `llms.txt` and `Curl.Mcp/Curl.Mcp.csproj` say `dnx Surly.Curl.Mcp` wherever they told the reader to run `dnx Curl.Mcp`.
- [ ] A search of the repository for `dnx Curl.Mcp`, outside `Tasks/`, `Audit/`, `Gap/` and the ADRs, finds nothing.
- [ ] If `llms.txt` is generated, the generator emits the new name, and the committed file matches its output.
- [ ] `dotnet build` is clean, and the fast tests for `Curl.Mcp.UnitTests` pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: a42e1a1b e4cfe50f 7b0d5525 94f0556a.
