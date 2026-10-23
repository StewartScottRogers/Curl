---
id: BL-3391
title: Vendor upstream curl 8.22.0's lib/libcurl.def, lib/optiontable.pl, lib/easyoptions.c and packages/OS400/ccsidcurl.c for the source-analysis cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/README.md, Curl.Conformance.UnitTests/UpstreamTestData/lib, Curl.Conformance.UnitTests/UpstreamTestData/packages]
requirement: none
created: 2026-10-23
completed:
---
# BL-3391 — Vendor upstream curl 8.22.0's lib/libcurl.def, lib/optiontable.pl, lib/easyoptions.c and packages/OS400/ccsidcurl.c for the source-analysis cases

## Goal

`Update-UpstreamTestData.ps1` also vendors curl 8.22.0's `lib/libcurl.def`, `lib/optiontable.pl`, `lib/easyoptions.c` and `packages/OS400/ccsidcurl.c` into `Curl.Conformance.UnitTests/UpstreamTestData`, and those four files are committed.

## Context

Upstream cases 1279 (`test1135.pl --heading=EXPORTS`, checks `lib/libcurl.def`), 1276 (`test1276.pl`, checks `lib/optiontable.pl` and `lib/easyoptions.c` against `curl.h`) and 1544 (`test1544.pl`, checks `packages/OS400/ccsidcurl.c` translates every string option) read these files in upstream's tree. They are in the curl 8.22.0 release tarball. The port tasks for those cases wait on this one so they do not each edit the update script. See ADR-0013 decision 3 (data is vendored and pinned; only the script downloads) and ADR-0749 (the vendored files stand for upstream's tree). The script already vendors `include/curl`, `docs` and three `src/tool_*.c` files; follow how it handles those.

## Acceptance criteria

- [ ] `Update-UpstreamTestData.ps1` extracts and copies the four files byte for byte, the way it handles its existing extra folders, and its help and `UpstreamTestData/README.md` name them.
- [ ] `UpstreamTestData/lib/libcurl.def`, `lib/optiontable.pl`, `lib/easyoptions.c` and `packages/OS400/ccsidcurl.c` exist and are byte-identical to the files in the `curl-8_22_0` source archive (state the check, e.g. SHA-256 of each against a fresh download).
- [ ] Running the script with `-Tag curl-8_22_0` again leaves `git status` clean for every other vendored file, and `UpstreamTestData/.gitattributes` keeps the new files `-text`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
