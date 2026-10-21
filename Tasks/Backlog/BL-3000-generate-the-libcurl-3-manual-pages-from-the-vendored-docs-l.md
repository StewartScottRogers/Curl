---
id: BL-3000
title: Generate the libcurl .3 manual pages from the vendored docs/libcurl Markdown and interpret test1222.pl's full deprecation check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2992]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1222Script.cs, Curl.Conformance.UnitTests/UpstreamTest1222ScriptTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3000 — Generate the libcurl .3 manual pages from the vendored docs/libcurl Markdown and interpret test1222.pl's full deprecation check

## Goal

Case 1222 runs curl 8.22.0's `tests/test1222.pl` deprecation check in full - headers, function man pages, option man pages and symbols-in-versions agree on every deprecation - instead of its "curl built without the libcurl manual" skip.

## Context

BL-2992 interpreted `test1222.pl` in `UpstreamTest1222Script` up to its `glob("$bldroot/docs/libcurl/*.3")` test: the `.3` pages are generated at build time by upstream's `scripts/cd2nroff` from `docs/libcurl/*.md` and `docs/libcurl/opts/*.md`, are in no release tarball, and so are not vendored (BL-2990), and with none the script prints its skip line and exits 0, as upstream's does in a build without the manual. With `.3` pages present the interpretation writes that the full check is not interpreted and exits 1. To finish: generate the `.3` pages beside the vendored Markdown (a C# port of the `cd2nroff` parts the check reads: `.IP` option paragraphs in `curl_easy_setopt.3` and `curl_easy_getinfo.3`, `.SH` DESCRIPTION/DEPRECATED sections, `.so man3/` includes), then port `scan_header` over the vendored `include/curl/*.h` (or `Libcurl`'s tables), `scan_man_for_opts`, `scan_man_page` and the table print of `test1222.pl` (https://github.com/curl/curl/blob/curl-8_22_0/tests/test1222.pl). Add the generator's output folder to `touches` when it is chosen.

## Acceptance criteria

- [ ] `UpstreamTest1222Script` carries out every check of `test1222.pl` when `.3` pages are present, each with a focused unit test.
- [ ] Case 1222 passes with `$env:CURL_UPSTREAM_CASE='1222'` with the `.3` pages present, not through the skip line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
