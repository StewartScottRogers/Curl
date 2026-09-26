---
id: BL-222
title: Add the Public Suffix List snapshot and its refresh script to Curl.Cookies
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-156]
touches: [Curl.Cookies.UnitLibrary, Curl.slnx, Update-PublicSuffixList.ps1]
requirement: none
created: 2026-09-26
completed:
---
# BL-222 — Add the Public Suffix List snapshot and its refresh script to Curl.Cookies

## Goal

A dated PSL snapshot is an embedded resource in `Curl.Cookies.UnitLibrary`, with its MPL-2.0 attribution, and `Update-PublicSuffixList.ps1` refreshes it.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q4a. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-156 ADR: embedded snapshot, parsed by hand, refreshed by a script in the `Scripts` solution folder, not at build time. Source https://publicsuffix.org/list/public_suffix_list.dat.

## Acceptance criteria

- [ ] The resource and attribution sit where the BL-156 ADR says; `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean.
- [ ] `Update-PublicSuffixList.ps1` downloads the list, writes the resource with the date, is ASCII-only, and is listed in the `Scripts` folder of `Curl.slnx`.

## Notes

- Split out of plan item BL-223 so the data and script land separately from the matching logic.
- Plan item: Q4a in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
