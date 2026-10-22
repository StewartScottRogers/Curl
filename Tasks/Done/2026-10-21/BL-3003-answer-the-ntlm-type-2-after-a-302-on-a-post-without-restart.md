---
id: BL-3003
title: Answer the NTLM type 2 after a 302 on a POST without restarting at type 1 so upstream case 1100 passes, and stop after the type 3 over HTTP/1.0 for case 159
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs, Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmAuthenticatedConnection.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3003 — Answer the NTLM type 2 after a 302 on a POST without restarting at type 1 so upstream case 1100 passes, and stop after the type 3 over HTTP/1.0 for case 159

## Goal

With `--ntlm -L`, the request after a redirect on the connection NTLM already authenticated goes out without an `Authorization` header, as curl 8.22.0 sends it, so upstream case 1100 passes; case 159 is split off as BL-3006.

## Context

Found by BL-2834 (ADR-0751), Debug now reported to both: test1100 differs at line 19 of <verify><protocol>: after the 302 Curl sends a new NTLM type 1 where curl sends the request without Authorization (expected "User-Agent: curl/8.22.0"). test159 ("known to fail" upstream, HTTP/1.0) differs at line 7: Curl sends another "GET /159 HTTP/1.0" where curl stops. Split if the two causes differ. Add focused unit tests (ADR-0482).

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-10-21 (lane 4): The two causes differ, so this task fixed test1100 and split test159 off as BL-3006 (curl stops on "Connection closure while negotiating auth (HTTP 1.0?)", a 401-retry decision in the HTTP handler).
- test1100's cause was in `Curl.Protocol.Http.UnitLibrary`, not the authenticator: a redirect hop's first request was planned before any challenge and got a fresh Type 1. curl's `Curl_output_ntlm` sends nothing once the connection's NTLM state is past Type 3. `HttpProtocolHandler.OnNtlmAuthenticatedConnection` now drops the Type 1 (and ends the probe, so a POST sends its body) when the hop's first request goes out on a reused connection and the earlier hop picked NTLM. Added `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` and the new test file to `touches`; no task in Doing on `origin/work/dark-factory` named them. `NtlmHttpAuthenticator.cs` did not need to change.
- Decision: limited to redirect hops (`RedirectsFollowed > 0`). A first try without that limit broke test2025, test2028 and test2031, where the next transfer of an easy handle (lib2023) starts NTLM again with Type 1. A new connection also starts again with Type 1, as curl's NTLM state belongs to the connection.
- Focused tests: `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmAuthenticatedConnection.cs` (5 tests). test1100 added to `PassingUpstreamCases.txt`. Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-21: Doing -> Done. A redirect hop on a connection NTLM already authenticated sends no new Type 1, so upstream case 1100 passes; case 159 split off as BL-3006
