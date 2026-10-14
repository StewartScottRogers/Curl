---
id: BL-2174
title: A Set-Cookie secure attribute after a TAB marks the cookie secure (upstream test2885)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed:
---
# BL-2174 — A Set-Cookie secure attribute after a TAB marks the cookie secure (upstream test2885)

## Goal

A `Set-Cookie` header whose `secure` attribute is preceded by a TAB marks the cookie secure, as curl 8.22.0 does, so upstream test2885 passes.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: test2885 ("Cookies with TAB before 'secure'") fails on its saved cookie jar, line 6: expected `cookie.example\tFALSE\t/\tTRUE\t0\tthisis\tSECRET`, got `...\tFALSE\t...` (the secure column). Start in `Curl.Cookies.UnitLibrary/SetCookieParser.cs` (attribute-name trimming); compare curl 8.22.0's `lib/cookie.c`.

## Acceptance criteria

- [ ] test2885 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Cookies.UnitTests` pins a TAB before `secure` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
