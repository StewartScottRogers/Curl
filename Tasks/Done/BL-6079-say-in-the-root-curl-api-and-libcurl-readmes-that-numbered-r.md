---
id: BL-6079
title: Say in the root, Curl.Api and Libcurl READMEs that numbered releases publish the packages to nuget.org, and name Surly.Curl
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Curl.Api/README.md, Libcurl/README.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6079 — Say in the root, Curl.Api and Libcurl READMEs that numbered releases publish the packages to nuget.org, and name Surly.Curl

## Goal

The root, `Curl.Api` and `Libcurl` READMEs say what is true of nuget.org now: numbered releases publish the packages there, `Surly.Curl` included; the rolling `preview` does not.

## Context

Since ADR-1443 (BL-2393) every `v*` tag pushes the packages to nuget.org (v0.4.0-preview.5 is there), and BL-6078 (ADR-1464) added the `Surly.Curl` tool. `README.md` ("Download and use") still says "Nothing is on nuget.org yet"; `Curl.Api/README.md` and `Libcurl/README.md` offer only the download-into-a-folder route. `DOWNLOAD.md` was corrected by BL-6078 and shows the wording.

## Acceptance criteria

- [x] `README.md` no longer says nothing is on nuget.org, and names `Surly.Curl` with `dnx Surly.Curl@<version> --yes -- <curl arguments>`.
- [x] `Curl.Api/README.md` and `Libcurl/README.md` say numbered releases are on nuget.org, keeping the folder route for the rolling `preview`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `README.md` "Download and use": the nuget.org sentence is replaced, `Surly.Curl` and its `dnx` line added, and the `Curl.Api` and `Libcurl` table rows now name both sources.
- Wording follows `DOWNLOAD.md` and ADR-1443 and ADR-1464. `Surly.Curl` is described as published from the first numbered release made after 2026-11-27, as `DOWNLOAD.md` says; nuget.org itself was not queried.
- The bottom section of both library READMEs is renamed "Get the package"; no link pointed at the old anchor.
- Checked 2026-11-27: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0.
- Filed BL-6080: `DOWNLOAD.md` still names v0.2.0-preview.1 as the newest numbered pre-release.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 3d587cf9 e6661b5b 3d836d5e.
- 2026-11-27: Doing -> Done. The root, Curl.Api and Libcurl READMEs say numbered releases publish the packages to nuget.org and the root README names Surly.Curl
