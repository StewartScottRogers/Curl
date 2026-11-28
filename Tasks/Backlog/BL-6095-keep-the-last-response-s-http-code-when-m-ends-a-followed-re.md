---
id: BL-6095
title: Keep the last response's %{http_code} when -m ends a followed redirect hop that got no response
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6092]
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerTimedOutHopResponseCodeTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6095 — Keep the last response's %{http_code} when -m ends a followed redirect hop that got no response

## Goal

Under `-L -m`, a transfer that `-m` ends on a followed redirect hop before any response arrives reports the earlier hop's `%{http_code}` (`302`), as curl does, instead of `000`.

## Context

Seen while measuring BL-6092 (2026-11-28). A loopback server answers a 302 to a listener that never answers; `-sS -L -m 2 -w "%{http_code}"` prints `302` from real curl (`C:\Program Files\Git\ucrt64\bin\curl.exe`, measured with `Record-CurlExchange.ps1`) and `000` from Curl. `RedirectFollower`'s merged report takes the last hop's `TransferReport.ResponseCode`. Check how curl answers `%{http_version}`, `%{content_type}` and `%header{...}` in the same case before changing them. A successful or otherwise failed hop is out of scope unless measuring shows the same difference.

## Acceptance criteria

- [ ] The `-L -m 2` repro prints `302` for `%{http_code}`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the response code (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
