---
id: BL-2666
title: Run upstream case 1686: Digest Authorization from a Libcurl handle separates its fields with a comma and no space
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2666 — Run upstream case 1686: Digest Authorization from a Libcurl handle separates its fields with a comma and no space

## Goal

Upstream case 1686 (lib1686, Digest authentication from a `Libcurl` handle to two origins and two users) passes through the runner and is on the ratchet.

## Context

Found by BL-2544, which ported lib1686 (`Curl.Conformance.LibTest.UnitLibrary/Lib1686.cs`). Measured on Windows: `<verify><protocol> differs at byte 138 (line 7): expected "Authorization: Digest username="alice", realm="my-backyard", nonce="314156295", uri="/api", response="4ecc00e567c37a9d537727890c2e5b32""`, got the same fields joined by `,` with no space. The response hash already matches; only the separator differs. curl 8.22.0's `lib/vauth/digest.c` joins the fields with `, `. Find where the Libcurl path builds the Digest header differently from the tool's (the tool's Digest cases pass), and make them one.

## Acceptance criteria

- [x] A `Libcurl` handle with `CURLOPT_HTTPAUTH` `CURLAUTH_DIGEST` sends `Authorization: Digest` fields separated by `, `, with a focused unit test in `Libcurl.UnitTests` (or the fixed library's own `.UnitTests`).
- [x] Case 1686 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- No product change was needed: BL-2635 (Done) gave the libtest engine `EngineDialing.UsesHandBuiltNtlm`, and `EngineComposition.MatchesSspiBuild` is false over a `HandBuiltNtlmSecurityContextFactory`, so the Digest answer already takes curl's own `, ` layout. `CURL_UPSTREAM_CASE=1686` passes through the runner on Windows (measured 2026-10-17).
- Production `Libcurl` on Windows keeps the SSPI comma-only layout on purpose: it matches curl's Schannel build (ADR-0472). The `, ` form is the non-SSPI build's, which the harness matches (ADR-0455).
- Focused test: `Curl.Engine.UnitTests` `ManagedCurlEngineTests.PerformAsync_DialingUsesHandBuiltNtlmAndDigest_SeparatesTheFieldsWithACommaAndASpace`, beside BL-2635's test, because `EngineDialing` is internal to `Curl.Engine` and not visible to `Libcurl.UnitTests`. Added `Curl.Engine.UnitTests` to `touches` for it; no other task was in Doing on origin/work/dark-factory. Platform-neutral: off Windows the layout is `, ` regardless.
- 1686 added to `PassingUpstreamCases.txt`. Build clean; fast tests green (0 failed).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 6efd3947 f58dcef2 3d836d5e.
- 2026-10-17: Doing -> Done. Upstream case 1686 passes through the runner and is on the ratchet; the libtest engine's Digest answer uses curl's own ', ' layout
