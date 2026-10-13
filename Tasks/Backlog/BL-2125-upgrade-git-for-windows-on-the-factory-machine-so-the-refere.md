---
id: BL-2125
title: Upgrade Git for Windows on the factory machine so the reference curl is 8.22.0
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Record-CurlExchange.ps1]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2125 — Upgrade Git for Windows on the factory machine so the reference curl is 8.22.0

## Goal

The factory machine's Git for Windows is 2.56.0 or later, so the matched Windows reference
build that `Record-CurlExchange.ps1` and the gap runs find is curl 8.22.0.

## Context

ADR-0479 step 3.1 (retarget to curl 8.22.0). On 2026-10-13 the machine had Git 2.55.0.windows.3,
whose `C:\Program Files\Git\mingw64\bin\curl.exe -V` says `curl 8.21.0 (x86_64-w64-mingw32)
libcurl/8.21.0 Schannel ...`. Git for Windows 2.56.0.windows.1 (2026-09-28) and later "Comes
with cURL v8.22.0"; the latest on 2026-10-13 was 2.56.0.windows.2. The `windows-latest` CI
image already has 2.56.0.

Interactive only: it installs software on the machine the lanes run on, and no lane should be
mid-measurement when the reference changes. Upgrade between shifts (for example
`winget upgrade --id Git.Git -e`), or ask Stewart to run the installer if it needs his
elevation. Nothing in the repository changes unless `Record-CurlExchange.ps1`'s lookup fails
on the new layout.

## Acceptance criteria

- [ ] `& 'C:\Program Files\Git\mingw64\bin\curl.exe' -V` on the factory machine prints a first line starting `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, recorded in this task's Notes with its full output.
- [ ] `Record-CurlExchange.ps1` still finds that curl and records an exchange with it (a plain GET whose request shows `User-Agent: curl/8.22.0`), recorded in Notes.

## Notes

## Log

- 2026-10-13: Created.
