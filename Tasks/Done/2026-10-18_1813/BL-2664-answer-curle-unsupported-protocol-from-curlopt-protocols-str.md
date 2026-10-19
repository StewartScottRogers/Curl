---
id: BL-2664
title: Answer CURLE_UNSUPPORTED_PROTOCOL from CURLOPT_PROTOCOLS_STR for an unknown protocol (case 1597)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl.UnitTests/Options/Network/EasyNetworkOptionSettersTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2664 — Answer CURLE_UNSUPPORTED_PROTOCOL from CURLOPT_PROTOCOLS_STR for an unknown protocol (case 1597)

## Goal

`curl_easy_setopt(CURLOPT_PROTOCOLS_STR)` answers as curl 8.22.0 does - `CURLE_UNSUPPORTED_PROTOCOL` for a name it does not know (`goobar`, `http `, ` http`, `http,http,ft`), `CURLE_BAD_FUNCTION_ARGUMENT` for an empty list (``, `,,`), `CURLE_OK` for known names in any case and `all` - so upstream case 1597 (lib1597) prints `Tested 15 strings`.

## Context

Found by BL-2543, which ported lib1597. The runner reports `<verify><stdout> differs at byte 0: expected "Tested 15 strings\n", got "unexpectedly 'goobar' returned 43\n"`: Libcurl's `SetProtocols` (`Libcurl/Options/Network/EasyNetworkOptionSetters.cs`) answers `CURLE_BAD_FUNCTION_ARGUMENT` for every list it rejects. Upstream: `lib/setopt.c` `protocol2num` in curl 8.22.0. `CURLOPT_REDIR_PROTOCOLS_STR` shares the parser; keep it right too.

## Acceptance criteria

- [x] Upstream case 1597 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Libcurl, Libcurl.UnitTests to files, by reading EasyNetworkOptionSetters.SetProtocols and SetRedirectProtocols, so it can run beside other Engine tasks.

- 2026-10-18: The fix itself landed under BL-2727 (SetProtocols already answers UnsupportedProtocol); this task held case 1597 on the ratchet and added goobar and padded-space rows to the Libcurl unit test.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 08788b59 48be2d2b 3d836d5e.
- 2026-10-18: Doing -> Done. Case 1597 held on the ratchet; unit rows added; build clean, fast tests green
