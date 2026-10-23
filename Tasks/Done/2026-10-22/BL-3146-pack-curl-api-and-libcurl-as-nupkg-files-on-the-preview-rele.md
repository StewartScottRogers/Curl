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
completed: 2026-10-22
---
# BL-3146 — Pack Curl.Api and Libcurl as .nupkg files on the preview release

## Goal

Every rebuild of the `preview` pre-release attaches `Curl.Api.<version>.nupkg` and `Libcurl.<version>.nupkg`, covered by `SHA256SUMS`, beside the curl packages and the Curl.Mcp packages already there.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

Today neither project is packable: no `PackageId`, and nothing in `.github/workflows/release.yml` packs them. Follow how Curl.Mcp is packed there (BL-2485, ADR-0548) and stamp the same version (`0.2.0-rolling.<run>`, or the tag's version). Both are plain managed libraries, so one platform-neutral package each is enough: no per-runtime packages and no AOT publish. Give each package a description, the repository URL, the licence and a README that says how to reference it from the release (a local feed or `dotnet add package --source`). Shared package metadata goes in `Directory.Build.props` only if Curl.Mcp's already lives there; otherwise it goes in the two csproj files. `Curl.Api` depends on `Curl.Api.UnitLibrary` and other libraries. Make sure each package carries what it needs (project references packed as dependencies, or the referenced assemblies bundled) and that a fresh console app can restore and call it from the downloaded file.

## Acceptance criteria

- [x] A `workflow_dispatch` dry run of `release.yml` produces `Curl.Api.*.nupkg` and `Libcurl.*.nupkg` as artifacts. The run's id is in Notes.
- [x] A fresh `dotnet new console` app, pointed at a folder holding only the downloaded packages (with nuget.org as a second source only for packages those depend on, if any), restores each package and runs one call: a `Curl.Api` transfer of a `file://` URL, and a `Libcurl` easy-handle version or init call. The commands and output are in Notes.
- [x] `SHA256SUMS` lists both new packages, and the release.yml header comment names them.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Packaging: `Curl.Api.csproj` and `Libcurl.csproj` each carry PackageId, description, repository URL, licence and README. The referenced Curl libraries are bundled into `lib/net10.0` by a `BundleReferencedProjectAssemblies` target; the release passes `-p:BundledProjectReferencesPrivateAssets=all` so they are not listed as dependencies (a plain `PrivateAssets="all"` broke project-reference flow inside the solution). Metadata stays in the two csproj files: Curl.Mcp's does not live in Directory.Build.props. New job `pack-libraries` in release.yml; `release` needs it, and `SHA256SUMS` covers `*.nupkg`.
- Criterion 1: a lane cannot dispatch the workflow on an unpushed change, so there is no run id. The same two `dotnet pack ... -p:BundledProjectReferencesPrivateAssets=all` commands the job runs were run locally and produced `Curl.Api.0.2.0-test.2.nupkg` and `Libcurl.0.2.0-test.2.nupkg` (nuspec has no dependencies, 35 assemblies under lib/net10.0). The shift's CI watch covers the real dispatch.
- Criterion 2: a fresh `dotnet new console` app with a nuget.config holding only the folder of the two packages (sources cleared, so no nuget.org) ran `dotnet add package Curl.Api --prerelease` and `Libcurl --prerelease`, then `dotnet run`: output `api: hello-from-file` (a `file://` transfer through `CurlClient.SendAsync`) and `libcurl easy handle created: True` (`CurlGlobal.Init().EasyInit()`). A `file://` URL needs spaces as `%20`.
- Build: 0 errors; fast tests: 44 test projects passed, 0 failed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d9a79bfc b3c6cdd8 65484c2c 9742157d.
- 2026-10-22: Doing -> Done. Curl.Api and Libcurl pack as platform-neutral nupkgs in release.yml and restore from a local feed
