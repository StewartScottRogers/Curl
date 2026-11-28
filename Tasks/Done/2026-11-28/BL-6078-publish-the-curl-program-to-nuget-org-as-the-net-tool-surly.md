---
id: BL-6078
title: Publish the curl program to nuget.org as the .NET tool Surly.Curl (command curl), native AOT per platform like Surly.Curl.Mcp
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Curl.Console.csproj, Curl.Console/README.md, .github/workflows/release.yml, .github/release/New-ResourcesJson.ps1, DOWNLOAD.md, Curl.Api.UnitTests/SurlyPackageMetadataTests.cs, Curl.Api.UnitTests/Curl.Api.UnitTests.csproj]
requirement: none
created: 2026-11-27
completed: 2026-11-27
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

- [x] `dotnet pack Curl.Console` makes `Surly.Curl` with a `DotnetToolSettings.xml` whose command is `curl`, and one per-platform native-AOT package per runtime, as for `Surly.Curl.Mcp`. A packed per-platform package's `curl --version` runs with no .NET runtime.
- [x] `release.yml` attaches and pushes all 16 packages on a `v*` tag; its package count check says 16.
- [x] The package metadata tests cover `Surly.Curl`; `--ai-help` and `curl --version` are unchanged.
- [x] An ADR, `Curl.Console`'s README and `DOWNLOAD.md` say how to run curl from nuget.org (`dnx Surly.Curl@<version> --yes -- <args>`).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design: ADR-1464. Done directly in the session (a packaging change, no C# production code), so no `/feature` sub-agents and no `Measure-CodeQuality.ps1` run: only the test file among the `.cs` files changed.
- `touches` widened (no other task in Doing on `origin/work/dark-factory` names any of them): `Curl.Console/README.md` (the package README), `Curl.Api.UnitTests/Curl.Api.UnitTests.csproj` (links the files the metadata tests read) and `.github/release/New-ResourcesJson.ps1` (it throws on a release file it does not know, so the new packages would have failed the `describe` job).
- Verified on Windows, version `0.0.3-lane.6078`: `dotnet pack Curl.Console` made the tool package (`tools/any/any/DotnetToolSettings.xml`, command `curl`, six runtime packages named); `-r win-x64` made the native package, with no `curl.dll`. Its `curl.exe --version` answered with `DOTNET_ROOT` naming no directory and only `System32` on PATH. `dnx Surly.Curl@<v> --yes --source <folder> -- --version` (cmd, and PowerShell with `'--'`) and `dotnet tool install --tool-path` both ran it. `release.yml`'s `pack-tool` check script passed on both packages; `New-ResourcesJson.ps1` and `Test-ResourcesJson.ps1` passed on them.
- The other five runtime packages cannot be built here (AOT does not cross-compile between operating systems; only win-x64 was packed). `pack-tool` packs each on its own OS, as `pack-mcp` does; `release.yml` runs it on a merge to master and on a pull request touching `Curl.Console` or the workflow.
- Found: in PowerShell a bare `--` never reaches `dnx` (`dnx.ps1`), so `-- --version` fails there; the documents and `resources.json` say to write `'--'`.
- The runtime packages leave out native symbols and XML documentation (win-x64: 43 MB to 15 MB).
- `curl --version` and `--ai-help`: no production code changed; the packed `curl --version` prints `curl 8.22.0 ...` as before, and the fast tests that pin both pass.
- Fast tests: 58934 passed, 0 failed, 363 skipped. `CurlMcpPackIntegrationTests` and `CurlMcpNativeAot*` (4) pass, and `dotnet pack Curl.Mcp -r win-x64` still works.
- `DOWNLOAD.md`'s "not on nuget.org" statements for Curl.Api, Libcurl and Curl.Mcp were false since BL-2393 and are corrected; the same in other READMEs is BL-6079.
- `Surly.Curl` reaches nuget.org only when a `v*` tag is next pushed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Created from surly-05's question for the nuget.org-only Surly book (Stewart, 2026-11-27).
- 2026-11-27: Backlog -> Doing. Criteria at claim: 462d4c6b 51db2f06 51965029 23d63879 3d836d5e.
- 2026-11-27: Doing -> Done. curl packs as the native-AOT .NET tool Surly.Curl (command curl); a v* tag attaches and pushes all 16 packages
