---
id: BL-2965
title: Vendor curl 8.22.0's tests/data/data1400.c to data1481.c so the --libcurl cases have their expected C
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData/data/data14xx.c, Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/README.md, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
lane: no
requirement: none
created: 2026-10-21
completed:
---
# BL-2965 — Vendor curl 8.22.0's tests/data/data1400.c to data1481.c so the --libcurl cases have their expected C

## Goal

`Curl.Conformance.UnitTests/UpstreamTestData/data/` holds curl 8.22.0's `tests/data/data1400.c`, `data1401.c` to `data1407.c`, `data1420.c`, `data1465.c` and `data1481.c` byte for byte, so upstream cases 1400 to 1407, 1420, 1465 and 1481 compare Curl's `--libcurl` output with their expected C, and every one that then passes on Windows is on `PassingUpstreamCases.txt`.

## Context

BL-2862 reported the `--libcurl` feature to the harness and taught it the `$_ = '' if /PATTERN/` strip line, so the eleven cases run. Each names its expected file as `%includetext %SRCDIR/data/data%TESTNUMBER.c%`, which is not vendored, so each fails at byte 0 ("expected the end"). Source: https://raw.githubusercontent.com/curl/curl/curl-8_22_0/tests/data/data1400.c (and the same for the others). BL-2862 compared the downloaded files by hand with Curl's output: 1400, 1401, 1402, 1403, 1404, 1406, 1420, 1465 and 1481 match after the strip lines; 1405 and 1407 differ in setopt order, which BL-2966 owns. Add the eleven names to `Update-UpstreamTestData.ps1`'s data-file list and to the README's data section. Lines end in LF; `.gitattributes` marks the folder `-text`.

Remove `lane: no` once a lane's permissions let it write under `Curl.Conformance.UnitTests/UpstreamTestData/data/`: the `Edit(./data/**)` and `Read(./data/**)` denies in `.claude/settings.json`, meant for the root `data/` folder, also refused BL-2862's lane every read, `cp` and `Copy-Item` into this folder on 2026-10-21. An interactive session can do it in a few minutes, or narrow those denies to the root folder.

## Acceptance criteria

- [ ] The eleven `data<n>.c` files are in `Curl.Conformance.UnitTests/UpstreamTestData/data/`, byte for byte as curl 8.22.0's.
- [ ] `Update-UpstreamTestData.ps1` copies them and the README's data section names them.
- [ ] Each of the eleven cases that passes with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows is on `PassingUpstreamCases.txt` (expected: 1400, 1401, 1402, 1403, 1404, 1406, 1420, 1465, 1481).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and CI is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-21: Created.
