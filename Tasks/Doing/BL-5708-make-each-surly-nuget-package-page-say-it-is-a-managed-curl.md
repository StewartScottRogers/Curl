---
id: BL-5708
title: Make each Surly NuGet package page say it is a managed curl reimplementation with no native libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5707]
touches: [Curl.Api/Curl.Api.csproj, Libcurl/Libcurl.csproj, Curl.Mcp/Curl.Mcp.csproj]
requirement: none
created: 2026-11-21
completed:
---
# BL-5708 — Make each Surly NuGet package page say it is a managed curl reimplementation with no native libcurl

## Goal

On nuget.org (and in Visual Studio's package search), each `Surly.*` package's title, first description line, tags and page make plain what sets it apart: a complete managed C# reimplementation of curl and libcurl for .NET 10, with no native libcurl, native-AOT ready, matching curl 8.22.0's behaviour.

## Context

Stewart, 2026-11-21, wants his packages differentiated from the other curl packages on nuget.org (native-libcurl wrappers such as `CurlThin` and `libcurl.NET.x64`, native binaries, and curl-command converters such as `CurlParser` and `HttpClientToCurl`). The package IDs get the `Surly.` prefix in the task this one depends on; this task does the package metadata. Today the csprojs carry `Authors`, a `Description`, `PackageTags`, `PackageReadmeFile` and `RepositoryUrl`, but the descriptions do not say "no native libcurl" up front, and there is no `Title`, icon, license expression or `PackageProjectUrl` that a search result shows.

For each of `Surly.Curl.Api`, `Surly.Libcurl` and `Surly.Curl.Mcp`:
- `Title`, for example "Surly Curl for .NET (managed)", "Surly Libcurl (managed libcurl API)", "Surly Curl MCP server".
- `Description` whose first sentence says it: for example "A complete managed C# reimplementation of curl/libcurl for .NET 10: no native libcurl, native-AOT ready, behaves like curl 8.22.0." Then what the package is for. Every statement true of the code (the say-what-it-does rule).
- `PackageTags` adding `managed; reimplementation; no-native; aot` to the existing ones.
- `PackageIcon` (one icon file shared by the three, kept in the repository and packed), `PackageLicenseExpression` matching the repository's licence, `PackageProjectUrl`, `RepositoryType`, and `Copyright`.
- The packed README starts with one paragraph saying the same, and the install line.
Shared values go in `Directory.Build.props` only if every packable project uses them (CLAUDE.md: shared build settings there); otherwise in each csproj. Package signing is out of scope (it needs a certificate Stewart would buy).

## Acceptance criteria

- [ ] Each of the three packed `.nuspec`s (read from the `.nupkg`) has a `title`, an `icon`, a `license` expression, a `projectUrl`, and a `description` whose first sentence says managed reimplementation and no native libcurl.
- [ ] Each package's tags include `managed`, `reimplementation`, `no-native` and `aot`.
- [ ] Each packed README opens with that statement and the `Surly.*` install line.
- [ ] A test (or the pack Integration test) reads the packed metadata and pins the points above.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created at Stewart's request (differentiate the packages on NuGet).
- 2026-11-21: Backlog -> Doing. Criteria at claim: 8a9f7bee ef2545e0 35f14f63 7cda2e82 3d836d5e.
