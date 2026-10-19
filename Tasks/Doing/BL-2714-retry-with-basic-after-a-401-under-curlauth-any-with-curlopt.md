---
id: BL-2714
title: Retry with Basic after a 401 under CURLAUTH_ANY with CURLOPT_REDIR_PROTOCOLS_STR https so upstream case 3101 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2714 — Retry with Basic after a 401 under CURLAUTH_ANY with CURLOPT_REDIR_PROTOCOLS_STR https so upstream case 3101 passes

## Goal

A transfer with `CURLOPT_HTTPAUTH` `CURLAUTH_ANY`, a user name and password, and `CURLOPT_REDIR_PROTOCOLS_STR` `https` answers a `401` offering `Basic` (and an unknown scheme) with a second request carrying `Authorization: Basic`, as curl 8.22.0 does, so upstream case 3101 passes.

## Context

Found by BL-2552, which ported lib3101 (`Curl.Conformance.LibTest.UnitLibrary/Lib3101.cs`). Running case 3101 through the runner gives `test3101 fails: <verify><protocol> differs at byte 57 (line 5): expected "GET /3101 HTTP/1.1\r\n", got the end`: Curl sends the first GET, gets the `401` with `WWW-Authenticate: Blackmagic ...` and `WWW-Authenticate: Basic ...`, and never sends the authenticated retry. The retry goes to the same `http://` URL, so a redirect-protocol list of `https` must not stop it: an authentication retry is not a redirect. Start where the engine decides to re-request after a `401` under picked authentication (`Curl.Engine.UnitLibrary`), and check whether the redirect-protocol filter is applied to that re-request. Measure real curl with `Record-CurlExchange.ps1` if the expected bytes are in doubt.

## Acceptance criteria

- [ ] With `CURLAUTH_ANY`, credentials and `CURLOPT_REDIR_PROTOCOLS_STR` `https`, a `401` offering `Basic` is followed by a retry with `Authorization: Basic`, pinned by a focused unit test in `Curl.Engine.UnitTests` (ADR-0482).
- [ ] Upstream case 3101 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 5efe25a4 f3b42217 0f653e7b.
