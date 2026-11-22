---
id: BL-5715
title: Say dotnet add package Surly.Curl.Api and Surly.Libcurl in llms.txt
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [llms.txt]
requirement: none
created: 2026-11-21
completed:
---
# BL-5715 — Say dotnet add package Surly.Curl.Api and Surly.Libcurl in llms.txt

## Goal

`llms.txt` tells an AI agent to install the .NET libraries by the package IDs they are published as, `Surly.Curl.Api` and `Surly.Libcurl`.

## Context

Found by the shift-end review (ADR-0710) of c05bbfc250...a0a785d6de, `pr-review-toolkit:code-reviewer` pass. BL-5714 changed
`llms.txt:16` to `dnx Surly.Curl.Mcp`, but left the next two lines of that same "Resources" list naming package IDs that
ADR-1333 retired:

- `llms.txt:17`: "`dotnet add package Curl.Api --prerelease`". `Curl.Api/Curl.Api.csproj:10` packs `PackageId` `Surly.Curl.Api`.
- `llms.txt:18`: "`dotnet add package Libcurl --prerelease`". `Libcurl/Libcurl.csproj:10` packs `PackageId` `Surly.Libcurl`.

Against a folder source that holds only the `Surly.*` nupkgs, both commands fail with "Unable to find package". `llms.txt`
is written for AI agents, which run the command as written. `README.md:50-51`, `Curl.Api/README.md` and `Libcurl/README.md`
already use the `Surly.` names. Keep the display names ("Curl.Api (kind `dotnet-library`)") as they are: they name the
projects, not the packages. `llms.txt` is hand-written (BL-5714's notes say there is no generator for it).

## Acceptance criteria

- [x] `llms.txt` says `dotnet add package Surly.Curl.Api --prerelease` and `dotnet add package Surly.Libcurl --prerelease`.
- [x] A search of the repository for `add package Curl.Api` and `add package Libcurl`, outside `Tasks/`, `Audit/`, `Gap/` and the ADRs, finds nothing.
- [x] `dotnet build` is clean.

## Notes

llms.txt lines 17-18 now use Surly.* package IDs; repo search finds no old commands; build clean.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: d879f2ec b92652c0 42b24d12.
