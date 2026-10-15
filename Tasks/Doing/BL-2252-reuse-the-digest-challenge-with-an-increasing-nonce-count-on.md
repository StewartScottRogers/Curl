---
id: BL-2252
title: Reuse the Digest challenge with an increasing nonce-count on a same-host --location redirect (test1286)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2252 — Reuse the Digest challenge with an increasing nonce-count on a same-host --location redirect (test1286)

## Goal

`curl -u auser:apasswd --location --digest URL`, answered 401 Digest then a 302 to a path on the same host, sends `Authorization: Digest ... nc=00000002 ...` on the redirected request, as curl does, so upstream case 1286 passes.

## Context

Found by BL-2238 (2026-10-14), which made the harness run test1286's Digest-field sorting strip program. With the strip carried out, test1286's first two requests match; the third, `GET /12860001` after the 302, has no `Authorization:` line from Curl, where curl 8.22.0 sends a Digest header reusing the first challenge's nonce with `nc=00000002` (curl's `lib/vauth/digest.c` keeps `digest->nc` and increments it per use; https://github.com/curl/curl/pull/1251). Start with the redirect follow in `Curl.Networking.UnitLibrary` and `DigestAuthenticator` / `DigestChallenge` in `Curl.Authentication.UnitLibrary`. Debug it with `CURL_UPSTREAM_CASE=1286` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] A same-host `--location` redirect after a successful Digest exchange sends Digest credentials with the challenge's nonce and the nonce-count increased by one, pinned by a focused unit test in the fixed library's `.UnitTests` project (ADR-0482).
- [ ] test1286 passes and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `Measure-CodeQuality.ps1` shows no member outside a gate for each library changed; build and fast tests green.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
