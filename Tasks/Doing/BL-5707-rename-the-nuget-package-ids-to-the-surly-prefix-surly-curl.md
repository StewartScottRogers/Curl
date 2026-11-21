---
id: BL-5707
title: Rename the NuGet package IDs to the Surly. prefix: Surly.Curl.Api, Surly.Libcurl, Surly.Curl.Mcp
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Curl.Api.csproj, Libcurl/Libcurl.csproj, Curl.Mcp/Curl.Mcp.csproj, .github/workflows/release.yml, DOWNLOAD.md]
requirement: none
created: 2026-11-21
completed:
---
# BL-5707 — Rename the NuGet package IDs to the Surly. prefix: Surly.Curl.Api, Surly.Libcurl, Surly.Curl.Mcp

## Goal

Every NuGet package Curl builds carries the `Surly.` prefix, so Curl's packages are told apart from the other curl packages on nuget.org: `Surly.Curl.Api`, `Surly.Libcurl`, `Surly.Curl.Mcp` and its runtime packages `Surly.Curl.Mcp.<rid>`. Namespaces, assembly names, project folders and command names do not change.

## Context

Stewart, 2026-11-21: "I need to make sure that my packages on NuGet are differentiated from other Curl implementations" and "Use Surly as the prefix". nuget.org holds native-libcurl wrappers (`CurlThin`, `libcurl.NET.x64`, `rmt_curl`), native binaries and curl-command converters; none is a managed reimplementation, and the bare IDs `Curl.Api`, `Libcurl` and `Curl.Mcp` read as generic (`Libcurl` especially as yet another native build). Stewart will ask nuget.org to reserve the `Surly.` prefix for his account, so only he can publish under it and the packages show the verified tick. Publishing to nuget.org itself stays on hold (BL-2393); this changes what the GitHub `preview` release and local packs produce.

| Today | New `PackageId` |
| --- | --- |
| `Curl.Api` | `Surly.Curl.Api` |
| `Libcurl` | `Surly.Libcurl` |
| `Curl.Mcp` (tool, command `curl-mcp`) | `Surly.Curl.Mcp` |
| `Curl.Mcp.<rid>` (6 runtime packages) | `Surly.Curl.Mcp.<rid>` |

Change: `<PackageId>` in `Curl.Api/Curl.Api.csproj`, `Libcurl/Libcurl.csproj`, `Curl.Mcp/Curl.Mcp.csproj` (check the runtime packages follow it); the package file names and checks in `.github/workflows/release.yml` (for example the `Curl.Mcp.$env:VERSION.nupkg` and `Curl.Mcp.$env:RID.$env:VERSION.nupkg` names); every install instruction (`dotnet add package ...`, `dotnet tool install ...`) in `DOWNLOAD.md`, the root `README.md`, `Curl.Api/README.md`, `Libcurl/README.md`, `Curl.Mcp/README.md` and `Documentation/`, and `--ai-help` if it names a package; any test that pins a package name (for example `CurlMcpPackIntegrationTests`). Do not change: namespaces (`Curl.Api`, `Libcurl`, `Curl.Mcp...`), assembly names, project folders, `Curl.slnx`, the commands `curl`, `curl-config`, `curl-mcp`. Record the naming in an ADR (decided by Stewart, recorded by Claude), including why namespaces stay.

## Acceptance criteria

- [ ] `dotnet pack` of `Curl.Api`, `Libcurl` and `Curl.Mcp` produces `Surly.Curl.Api.<v>.nupkg`, `Surly.Libcurl.<v>.nupkg`, `Surly.Curl.Mcp.<v>.nupkg`, and the Mcp runtime packs `Surly.Curl.Mcp.<rid>.<v>.nupkg`; no package without the prefix is produced.
- [ ] `.github/workflows/release.yml` builds, names and checksums the new files, and its own checks pass (a dry run or the next master push's release run; the Notes record which).
- [ ] A project installing `Surly.Curl.Api` and `Surly.Libcurl` from a local feed compiles with the unchanged `using Curl.Api;` / `using Libcurl;`, and `dotnet tool install Surly.Curl.Mcp` from a local feed installs a working `curl-mcp`; the Notes record the runs.
- [ ] No install instruction in the repository names an unprefixed package (a search for `dotnet add package Curl`, `add package Libcurl` and `tool install Curl.Mcp` finds nothing); an ADR records the names.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created at Stewart's request ("Use Surly as the prefix").
- 2026-11-21: Backlog -> Doing. Criteria at claim: 739be49e 3bccf144 3037f02d 7781b8cb 3d836d5e.
