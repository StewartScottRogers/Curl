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
completed: 2026-10-13
---
# BL-2174 — A Set-Cookie secure attribute after a TAB marks the cookie secure (upstream test2885)

## Goal

A `Set-Cookie` header whose `secure` attribute is preceded by a TAB marks the cookie secure, as curl 8.22.0 does, so upstream test2885 passes.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: test2885 ("Cookies with TAB before 'secure'") fails on its saved cookie jar, line 6: expected `cookie.example\tFALSE\t/\tTRUE\t0\tthisis\tSECRET`, got `...\tFALSE\t...` (the secure column). Start in `Curl.Cookies.UnitLibrary/SetCookieParser.cs` (attribute-name trimming); compare curl 8.22.0's `lib/cookie.c`.

## Acceptance criteria

- [x] test2885 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Curl.Cookies.UnitTests` pins a TAB before `secure` (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: curl 8.22.0's `parse_cookie_header` (lib/cookie.c at `curl-8_22_0`) reads a part's name with
  `curlx_str_cspn(&ptr, &name, ";\r\n=")` and then `curlx_str_trimblanks`; 8.21.0's set also held `\t`,
  so a tab ended the name and the reading. `SetCookieParser` now ends a name only at `;` or `=`, trims
  spaces and tabs from both ends, and refuses a tab left inside the trimmed name as
  `invalid octets in name, cookie dropped` (8.22.0's `invalid_octets` rejects every byte below 0x20).
- A first attempt (passing blanks after each `;`) broke `u5=v; =x; Secure`; the source showed the real rule.
- Tests that pinned the 8.21.0 tab rule were updated to 8.22.0's: `Secure\tx`, `Fo\to=x`, `n\tb=v`,
  `Pa\tth=/` refuse; `Path\t=/x`, `t8\t=v`, `\tt9=v`, `;\tSecure` now read the trimmed name.
- No ADR: this follows upstream's own change and test, no design choice was made.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. A Set-Cookie name now runs to ; or = and loses blanks at both ends, as curl 8.22.0 does; test2885 passes and is ratcheted
