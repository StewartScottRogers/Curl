---
id: BL-3000
title: Generate the libcurl .3 manual pages from the vendored docs/libcurl Markdown and interpret test1222.pl's full deprecation check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2992]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1222Script.cs, Curl.Conformance.UnitTests/UpstreamTest1222ScriptTests.cs, Curl.Conformance.UnitLibrary/UpstreamCd2nroff.cs, Curl.Conformance.UnitTests/UpstreamCd2nroffTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3000 — Generate the libcurl .3 manual pages from the vendored docs/libcurl Markdown and interpret test1222.pl's full deprecation check

## Goal

Case 1222 runs curl 8.22.0's `tests/test1222.pl` deprecation check in full - headers, function man pages, option man pages and symbols-in-versions agree on every deprecation - instead of its "curl built without the libcurl manual" skip.

## Context

BL-2992 interpreted `test1222.pl` in `UpstreamTest1222Script` up to its `glob("$bldroot/docs/libcurl/*.3")` test: the `.3` pages are generated at build time by upstream's `scripts/cd2nroff` from `docs/libcurl/*.md` and `docs/libcurl/opts/*.md`, are in no release tarball, and so are not vendored (BL-2990), and with none the script prints its skip line and exits 0, as upstream's does in a build without the manual. With `.3` pages present the interpretation writes that the full check is not interpreted and exits 1. To finish: generate the `.3` pages beside the vendored Markdown (a C# port of the `cd2nroff` parts the check reads: `.IP` option paragraphs in `curl_easy_setopt.3` and `curl_easy_getinfo.3`, `.SH` DESCRIPTION/DEPRECATED sections, `.so man3/` includes), then port `scan_header` over the vendored `include/curl/*.h` (or `Libcurl`'s tables), `scan_man_for_opts`, `scan_man_page` and the table print of `test1222.pl` (https://github.com/curl/curl/blob/curl-8_22_0/tests/test1222.pl). Add the generator's output folder to `touches` when it is chosen.

## Acceptance criteria

- [x] `UpstreamTest1222Script` carries out every check of `test1222.pl` when `.3` pages are present, each with a focused unit test.
- [x] Case 1222 passes with `$env:CURL_UPSTREAM_CASE='1222'` with the `.3` pages present, not through the skip line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Decision (ADR-0751): the `.3` pages are converted in memory by the new `UpstreamCd2nroff` (a port of `scripts/cd2nroff`) from `ROOT/docs/libcurl` when `BUILDROOT/docs/libcurl` has no built `.3` pages. Nothing is generated on disk, so there is no output folder to add to `touches`. The two new files (`UpstreamCd2nroff.cs` and its tests) were added to `touches` because no other task in Doing on `origin/work/dark-factory` names them.
- `UpstreamTest1222Script` now ports `scan_header`, `scan_man_page` (with `.so man3/` includes), `scan_man_for_opts` (whose findings land in the function-page table, as upstream's do), symbols-in-versions reading and the table print. Perl's undefined hash and readdir order is replaced by ordinal order.
- Measured: `UpstreamTest1222ScriptTests.Check_VendoredRelease_FindsTheSourcesAgreeOverItsConvertedManual` runs the full check on the vendored 8.22.0 data and gets exit 0 with empty output. Case 1222 passes with `CURL_UPSTREAM_CASE=1222`. Measure-CodeQuality on `Curl.Conformance.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: d5897245 ca1dc66a 1861eadd.
- 2026-10-21: Doing -> Done. Case 1222 runs test1222.pl's full deprecation check over a libcurl manual converted from the vendored Markdown
