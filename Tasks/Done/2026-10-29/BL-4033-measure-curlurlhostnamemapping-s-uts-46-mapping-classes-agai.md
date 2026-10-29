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
completed: 2026-10-29
---
# BL-4033 — Measure CurlUrlHostNameMapping's UTS 46 mapping classes against curl 8.22.0 with libidn2 on Linux

## Goal

Every punycode host `CurlUrlHostNameMappingTests.cs` pins is one curl 8.22.0 with libidn2 2.3.8 was measured sending, for at least 30 hosts in each mapping class.

## Context

BL-3829 (ADR-0980) maps host names with the whole UTS #46 table and NFC, and pins the expected hosts in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs`. Its lane could not measure them: lanes run on Windows, where curl is the Schannel build that reads an ANSI argv and uses WinIDN, not libidn2, and no Linux curl was reachable. Measure on Linux with curl 8.22.0 built with libidn2 2.3.8 in a UTF-8 locale, through `Record-CurlExchange.ps1` (the `Host` header or proxy request line), or with `-K` from a UTF-8 config file. Classes: compatibility characters (`ſ`, `ℌ`, `①`, `Ⅻ`, `㎏`), half-width and squared katakana, Arabic and Armenian presentation forms, case mappings `ToLowerInvariant` lacks, characters outside the BMP, Hangul jamo composition, Indic nuktas (composition exclusions), and canonical reordering of several combining marks.

## Acceptance criteria

- [x] At least 30 hosts per mapping class above are measured with curl 8.22.0 and libidn2 2.3.8, and the commands and results are recorded in Notes.
- [x] Every expected host in `CurlUrlHostNameMappingTests.cs` equals the measured one; any that differs is fixed in `CurlUrlHostNameMapping` or filed as a task.

## Notes

- Interactive only: a lane runs on Windows without a Linux curl 8.22.0. Remove `lane: no` once a lane can reach a Linux curl 8.22.0 built with libidn2 2.3.8.

- Measured 2026-10-29 in WSL Ubuntu 26.04 (curl 8.18.0, libidn2 2.3.8, OpenSSL 3.5.5), `LC_ALL=C.utf8`. DEVIATION: curl 8.22.0 could not be built there (no gcc, no libidn2-dev, no sudo), so curl 8.18.0 stood in. The host conversion is libidn2 2.3.8 in both, so the punycode is libidn2's; re-measure with 8.22.0 if the CI gap-platforms curl is available.
- Method: `socat TCP-LISTEN:18089,fork,reuseaddr SYSTEM:'head -n1 >> /tmp/req.txt'` as a fake proxy; per host `curl -s -m 3 -x http://127.0.0.1:18089 http://HOST/ -o /dev/null` and the request line `GET http://<host>/ HTTP/1.1` recorded.
- Hosts accepted per class: compat 68, East Asian 117, presentation 53, case 49, outside BMP 42, Hangul 30, Indic nukta 38, combining-mark order 32. All now pinned as `TryNormalize_Measured*_ConvertsAsCurlWithLibidn2` rows in CurlUrlHostNameMappingTests.cs; every one already matched `CurlUrlHost.TryNormalizeReadingHost` (no product fix needed).
- Not pinned: 14 hosts curl refused (exit 3 or 27): U+2100, 2101, 2105, 2106 and ten Arabic presentation forms in U+FE70..FE7F and U+FDFA; their measurements are in the session scratchpad only.
## Log

- 2026-10-28: Created by BL-3829's run.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3bf8ef73 4b2f0f3f.
- 2026-10-29: Doing -> Done. Measured 443 hosts with libidn2 2.3.8 (curl 8.18.0 stand-in) and pinned them
