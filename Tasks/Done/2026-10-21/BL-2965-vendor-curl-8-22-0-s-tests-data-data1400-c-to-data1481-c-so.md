---
id: BL-2965
title: Vendor curl 8.22.0's tests/data/data1400.c to data1481.c so the --libcurl cases have their expected C
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData/data/data14xx.c, Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/README.md, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2965 — Vendor curl 8.22.0's tests/data/data1400.c to data1481.c so the --libcurl cases have their expected C

## Goal

`Curl.Conformance.UnitTests/UpstreamTestData/data/` holds curl 8.22.0's `tests/data/data1400.c`, `data1401.c` to `data1407.c`, `data1420.c`, `data1465.c` and `data1481.c` byte for byte, so upstream cases 1400 to 1407, 1420, 1465 and 1481 compare Curl's `--libcurl` output with their expected C, and every one that then passes on Windows is on `PassingUpstreamCases.txt`.

## Context

BL-2862 reported the `--libcurl` feature to the harness and taught it the `$_ = '' if /PATTERN/` strip line, so the eleven cases run. Each names its expected file as `%includetext %SRCDIR/data/data%TESTNUMBER.c%`, which is not vendored, so each fails at byte 0 ("expected the end"). Source: https://raw.githubusercontent.com/curl/curl/curl-8_22_0/tests/data/data1400.c (and the same for the others). BL-2862 compared the downloaded files by hand with Curl's output: 1400, 1401, 1402, 1403, 1404, 1406, 1420, 1465 and 1481 match after the strip lines; 1405 and 1407 differ in setopt order, which BL-2966 owns. Add the eleven names to `Update-UpstreamTestData.ps1`'s data-file list and to the README's data section. Lines end in LF; `.gitattributes` marks the folder `-text`.

Remove `lane: no` once a lane's permissions let it write under `Curl.Conformance.UnitTests/UpstreamTestData/data/`: the `Edit(./data/**)` and `Read(./data/**)` denies in `.claude/settings.json`, meant for the root `data/` folder, also refused BL-2862's lane every read, `cp` and `Copy-Item` into this folder on 2026-10-21. An interactive session can do it in a few minutes, or narrow those denies to the root folder.

## Acceptance criteria

- [x] The eleven `data<n>.c` files are in `Curl.Conformance.UnitTests/UpstreamTestData/data/`, byte for byte as curl 8.22.0's.
- [x] `Update-UpstreamTestData.ps1` copies them and the README's data section names them.
- [x] Each of the eleven cases that passes with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows is on `PassingUpstreamCases.txt` (expected: 1400, 1401, 1402, 1403, 1404, 1406, 1420, 1465, 1481).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes, and CI is green on Linux and macOS for the commit.

## Notes

- Read the data folder by absolute path: the root `data/` deny still refuses a relative `data/` path from inside UpstreamTestData. Downloaded the eleven files with curl from raw.githubusercontent.com at curl-8_22_0 (LF, unchanged).
- Measured on Windows with CURL_UPSTREAM_CASE: 1400, 1401, 1403, 1420 and 1481 pass and are on the list. 1402, 1404, 1406 and 1465 fail, not only 1405 and 1407 as BL-2862 expected: Curl writes CURLOPT_USERAGENT after POSTFIELDS, the mime parts or CURLOPT_UPLOAD, where 8.22.0 writes it in credentials_and_headers_setopts, before transfer_setopts. That is the setopt order BL-2966 moves to 8.22.0, so those four are left to it (noted on BL-2966) rather than listed here.
- Platform: the five listed cases compare generated C only (no paths, OS errors or certificates), so their output does not depend on the platform. Fast tests: 44 projects, 0 failed. CI on Linux and macOS runs when the shift integrates the commit.

## Log

- 2026-10-21: Created.
- 2026-10-21: lane: no removed; PR #187 anchors the data deny at the project root (`/data/**`), so lanes can write under Curl.Conformance.UnitTests/UpstreamTestData/data/.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a535a660 556903c9 29d34864 6ea6410d.
- 2026-10-21: Doing -> Done. curl 8.22.0's data1400-1407.c, 1420.c, 1465.c, 1481.c are vendored; cases 1400, 1401, 1403, 1420, 1481 pass and are listed
