---
id: BL-6078
title: Publish the curl program to nuget.org as the .NET tool Surly.Curl (command curl), native AOT per platform like Surly.Curl.Mcp
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Curl.Console.csproj, .github/workflows/release.yml, DOWNLOAD.md, Curl.Api.UnitTests/SurlyPackageMetadataTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6078 — Publish the curl program to nuget.org as the .NET tool Surly.Curl (command curl), native AOT per platform like Surly.Curl.Mcp

## Goal

A reader with only .NET and nuget.org can run Curl's curl program with `dnx Surly.Curl@<version> --yes -- <curl arguments>`, or install it with `dotnet tool install -g Surly.Curl --prerelease` and run `curl`. Every `v*` tag publishes it to nuget.org beside the nine packages there today.

## Context

Stewart, 2026-11-27: the Surly book follows the nuget.org packages, not the repositories. Every recipe must run for a reader who has only .NET and nuget.org. The book's console recipes need the curl program, but today only `Surly.Curl.Mcp` (command `curl-mcp`), `Surly.Curl.Api` and `Surly.Libcurl` are on nuget.org; the curl executable ships only as GitHub release archives.

Decided by Claude under Stewart's delegation (record it in an ADR): package `Curl.Console` as the tool `Surly.Curl`, with command name `curl`, built the same way as `Surly.Curl.Mcp`.
- `Curl.Mcp.csproj` sets `PackAsTool`, `ToolCommandName` and `RuntimeIdentifiers` for six platforms, and `release.yml`'s `pack-mcp` job packs one native-AOT package per runtime plus the pointer package. Copy that shape, so `curl` stays the native binary the drop-in promise rests on, not a framework-dependent one.
- The command name `curl` matches the program. Installing it globally can shadow the system curl on PATH; `dnx` runs it without installing. Say so in the package README.

Also:
- `release.yml`'s nuget.org step checks for exactly nine packages. Make it 16: one `Surly.Curl` pointer package and six per-platform packages.
- The `Surly` package metadata tests (`Curl.Api.UnitTests/SurlyPackageMetadataTests.cs`) cover the new project: title, icon, embedded `LICENSE.txt`, project URL.
- `DOWNLOAD.md` names the nuget.org route beside the archives.

## Acceptance criteria

- [ ] `dotnet pack Curl.Console` makes `Surly.Curl` with a `DotnetToolSettings.xml` whose command is `curl`, and one per-platform native-AOT package per runtime, as for `Surly.Curl.Mcp`. A packed per-platform package's `curl --version` runs with no .NET runtime.
- [ ] `release.yml` attaches and pushes all 16 packages on a `v*` tag; its package count check says 16.
- [ ] The package metadata tests cover `Surly.Curl`; `--ai-help` and `curl --version` are unchanged.
- [ ] An ADR, `Curl.Console`'s README and `DOWNLOAD.md` say how to run curl from nuget.org (`dnx Surly.Curl@<version> --yes -- <args>`).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Created from surly-05's question for the nuget.org-only Surly book (Stewart, 2026-11-27).
