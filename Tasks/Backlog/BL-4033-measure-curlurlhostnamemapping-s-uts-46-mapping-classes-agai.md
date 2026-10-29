---
id: BL-4033
title: Measure CurlUrlHostNameMapping's UTS 46 mapping classes against curl 8.22.0 with libidn2 on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs]
lane: no
requirement: none
created: 2026-10-28
completed:
---
# BL-4033 — Measure CurlUrlHostNameMapping's UTS 46 mapping classes against curl 8.22.0 with libidn2 on Linux

## Goal

Every punycode host `CurlUrlHostNameMappingTests.cs` pins is one curl 8.22.0 with libidn2 2.3.8 was measured sending, for at least 30 hosts in each mapping class.

## Context

BL-3829 (ADR-0980) maps host names with the whole UTS #46 table and NFC, and pins the expected hosts in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs`. Its lane could not measure them: lanes run on Windows, where curl is the Schannel build that reads an ANSI argv and uses WinIDN, not libidn2, and no Linux curl was reachable. Measure on Linux with curl 8.22.0 built with libidn2 2.3.8 in a UTF-8 locale, through `Record-CurlExchange.ps1` (the `Host` header or proxy request line), or with `-K` from a UTF-8 config file. Classes: compatibility characters (`ſ`, `ℌ`, `①`, `Ⅻ`, `㎏`), half-width and squared katakana, Arabic and Armenian presentation forms, case mappings `ToLowerInvariant` lacks, characters outside the BMP, Hangul jamo composition, Indic nuktas (composition exclusions), and canonical reordering of several combining marks.

## Acceptance criteria

- [ ] At least 30 hosts per mapping class above are measured with curl 8.22.0 and libidn2 2.3.8, and the commands and results are recorded in Notes.
- [ ] Every expected host in `CurlUrlHostNameMappingTests.cs` equals the measured one; any that differs is fixed in `CurlUrlHostNameMapping` or filed as a task.

## Notes

- Interactive only: a lane runs on Windows without a Linux curl 8.22.0. Remove `lane: no` once a lane can reach a Linux curl 8.22.0 built with libidn2 2.3.8.

## Log

- 2026-10-28: Created by BL-3829's run.
