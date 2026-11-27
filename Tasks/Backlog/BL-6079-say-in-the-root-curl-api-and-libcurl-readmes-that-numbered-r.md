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
completed:
---
# BL-6079 — Say in the root, Curl.Api and Libcurl READMEs that numbered releases publish the packages to nuget.org, and name Surly.Curl

## Goal

The root, `Curl.Api` and `Libcurl` READMEs say what is true of nuget.org now: numbered releases publish the packages there, `Surly.Curl` included; the rolling `preview` does not.

## Context

Since ADR-1443 (BL-2393) every `v*` tag pushes the packages to nuget.org (v0.4.0-preview.5 is there), and BL-6078 (ADR-1464) added the `Surly.Curl` tool. `README.md` ("Download and use") still says "Nothing is on nuget.org yet"; `Curl.Api/README.md` and `Libcurl/README.md` offer only the download-into-a-folder route. `DOWNLOAD.md` was corrected by BL-6078 and shows the wording.

## Acceptance criteria

- [ ] `README.md` no longer says nothing is on nuget.org, and names `Surly.Curl` with `dnx Surly.Curl@<version> --yes -- <curl arguments>`.
- [ ] `Curl.Api/README.md` and `Libcurl/README.md` say numbered releases are on nuget.org, keeping the folder route for the rolling `preview`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
