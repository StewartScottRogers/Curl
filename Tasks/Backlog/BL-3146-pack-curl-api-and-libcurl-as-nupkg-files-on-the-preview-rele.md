---
id: BL-3146
title: Pack Curl.Api and Libcurl as .nupkg files on the preview release
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api/Curl.Api.csproj, Libcurl/Libcurl.csproj, .github/workflows/release.yml]
requirement: none
created: 2026-10-22
completed:
---
# BL-3146 — Pack Curl.Api and Libcurl as .nupkg files on the preview release

## Goal

Every rebuild of the `preview` pre-release attaches `Curl.Api.<version>.nupkg` and `Libcurl.<version>.nupkg`, covered by `SHA256SUMS`, beside the curl packages and the Curl.Mcp packages already there.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

Today neither project is packable: no `PackageId`, and nothing in `.github/workflows/release.yml` packs them. Follow how Curl.Mcp is packed there (BL-2485, ADR-0548) and stamp the same version (`0.2.0-rolling.<run>`, or the tag's version). Both are plain managed libraries, so one platform-neutral package each is enough: no per-runtime packages and no AOT publish. Give each package a description, the repository URL, the licence and a README that says how to reference it from the release (a local feed or `dotnet add package --source`). Shared package metadata goes in `Directory.Build.props` only if Curl.Mcp's already lives there; otherwise it goes in the two csproj files. `Curl.Api` depends on `Curl.Api.UnitLibrary` and other libraries. Make sure each package carries what it needs (project references packed as dependencies, or the referenced assemblies bundled) and that a fresh console app can restore and call it from the downloaded file.

## Acceptance criteria

- [ ] A `workflow_dispatch` dry run of `release.yml` produces `Curl.Api.*.nupkg` and `Libcurl.*.nupkg` as artifacts. The run's id is in Notes.
- [ ] A fresh `dotnet new console` app, pointed at a folder holding only the downloaded packages (with nuget.org as a second source only for packages those depend on, if any), restores each package and runs one call: a `Curl.Api` transfer of a `file://` URL, and a `Libcurl` easy-handle version or init call. The commands and output are in Notes.
- [ ] `SHA256SUMS` lists both new packages, and the release.yml header comment names them.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
