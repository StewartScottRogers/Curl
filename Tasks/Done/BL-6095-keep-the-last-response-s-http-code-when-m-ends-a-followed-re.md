---
id: BL-6095
title: Keep the last response's %{http_code} when -m ends a followed redirect hop that got no response
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6092]
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitLibrary/CLAUDE.md, Curl.Core.UnitTests/RedirectFollowerTimedOutHopResponseCodeTests.cs]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-6095 — Keep the last response's %{http_code} when -m ends a followed redirect hop that got no response

## Goal

Under `-L -m`, a transfer that `-m` ends on a followed redirect hop before any response arrives reports the earlier hop's `%{http_code}` (`302`), as curl does, instead of `000`.

## Context

Seen while measuring BL-6092 (2026-11-28). A loopback server answers a 302 to a listener that never answers; `-sS -L -m 2 -w "%{http_code}"` prints `302` from real curl (`C:\Program Files\Git\ucrt64\bin\curl.exe`, measured with `Record-CurlExchange.ps1`) and `000` from Curl. `RedirectFollower`'s merged report takes the last hop's `TransferReport.ResponseCode`. Check how curl answers `%{http_version}`, `%{content_type}` and `%header{...}` in the same case before changing them. A successful or otherwise failed hop is out of scope unless measuring shows the same difference.

## Acceptance criteria

- [x] The `-L -m 2` repro prints `302` for `%{http_code}`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the response code (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-28: curl keeps `302`, `%{http_version}` `1.1`, `%{content_type}` and `%{num_headers}` `4`, with `%header{x-hop}` empty and `%{header_json}` `{}`, both when `-m` ends the hop (exit 28) and when its connection is refused (exit 7). Curl printed `000`/`0`/empty on the timeout and `302`/`0`/empty on the refusal.
- Fixed code, version and content type for any failed hop with no HTTP response, since the refusal measured the same difference (ADR-1473). A successful hop with no HTTP response was not measured and is unchanged.
- After the fix both repros match curl except `%{num_headers}`, which needs a `TransferReport` property outside this task: filed as BL-6096.
- Added `Curl.Core.UnitLibrary/CLAUDE.md` to `touches` for its sentence on the merged report; BL-6093, the other task in Doing, does not name it.
- Quality for `Curl.Core.UnitLibrary`: 100% line and branch, 0 failing members.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing. Criteria at claim: 251413b1 dde3d256 3d836d5e.
- 2026-11-28: Doing -> Done. A followed redirect hop that fails with no response keeps the followed response's code, HTTP version and content type (ADR-1473).
