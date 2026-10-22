---
id: BL-2134
title: Fix CI failure test798 on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2134 — Fix CI failure test798 on Linux and macOS and Windows

## Goal

`test798` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test798` failed on Linux and macOS and Windows in CI run 38431484503 (https://github.com/StewartScottRogers/Curl/actions/runs/38431484503). First failing commit: 48919fca.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38431484503 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test798` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-2126 re-vendored upstream's tests/data at curl-8_22_0. In 8.22.0 test798 needs `PSL` and
  expects `Domain=localhost` from host `localhost` to be kept host-only (`localhost FALSE`), not
  `.localhost TRUE` as 8.21.0 wrote it: a `Domain` that is a public suffix and names the request host
  itself becomes a host-only cookie (RFC 6265 section 5.3 step 5). `localhost` is a public suffix by
  libpsl's implicit `*` rule.
- Fix: `PublicSuffixList.IsPublicSuffixNamingTheHost` and `CookieStore.StoreReceived` clear
  `IncludesSubdomains` for such a cookie. The rule is pinned to upstream's 8.22.0 test data (test798,
  test1136), since only 8.21.0 is installed to measure; no ADR, as it follows the targeted version.
- Touches: added `Curl.Cookies.UnitLibrary` and `Curl.Cookies.UnitTests` - the behaviour lives there; no
  other task in Doing on origin/work/dark-factory named them.
- `NetscapeCookieFileTests.WriteCookieJar_ReceivedCookies_MatchesCurlsJarByteForByte` pinned 8.21.0's
  `.localhost TRUE` for `d=3; Domain=localhost`; now 8.22.0's `localhost FALSE`.
- The same change makes test1136 (BL-2135) pass. test1462 (BL-2136) still fails; it is help output,
  unrelated, and left to BL-2136.
- Local runs: a stale `bin` copy of UpstreamTestData (PreserveNewest) made test798 compare against the
  8.21.0 data until the sources were touched; CI builds clean, so it is unaffected.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. test798 passes: a public-suffix Domain naming the host is kept host-only, as curl 8.22.0
