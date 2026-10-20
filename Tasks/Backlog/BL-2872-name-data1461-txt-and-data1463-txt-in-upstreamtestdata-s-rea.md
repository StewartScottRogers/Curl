---
id: BL-2872
title: Name data1461.txt and data1463.txt in UpstreamTestData's README data section
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData/README.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2872 — Name data1461.txt and data1463.txt in UpstreamTestData's README data section

## Goal

`Curl.Conformance.UnitTests/UpstreamTestData/README.md`'s `## data` section names every file in `data/`, so it is true again.

## Context

BL-2830 vendored curl 8.22.0's `tests/data/data1461.txt` and `data1463.txt` into `UpstreamTestData/data/`, byte for byte from https://github.com/curl/curl/tree/curl-8_22_0/tests/data, so the `%includetext %SRCDIR/data/data1461.txt%` of cases 1461, 1463 and 1464 reads curl's `--help` and `--help file` text. The README's `## data` section still says the folder holds only the two httpsig key files. BL-2830 left the README alone because BL-2864 (in Doing then) touched it; do this once BL-2864 is Done. If `Update-UpstreamTestData.ps1` refreshes `data/`, make it fetch these two files too.

## Acceptance criteria

- [ ] The README's `## data` section names `data1461.txt` and `data1463.txt` and the cases that read them (1461, 1463, 1464).
- [ ] `Update-UpstreamTestData.ps1` keeps the two files when it refreshes the folder, or the README says they are added by hand.

## Notes

## Log

- 2026-10-19: Created.
