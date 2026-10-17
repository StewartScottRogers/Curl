---
id: BL-2666
title: Run upstream case 1686: Digest Authorization from a Libcurl handle separates its fields with a comma and no space
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2666 — Run upstream case 1686: Digest Authorization from a Libcurl handle separates its fields with a comma and no space

## Goal

Upstream case 1686 (lib1686, Digest authentication from a `Libcurl` handle to two origins and two users) passes through the runner and is on the ratchet.

## Context

Found by BL-2544, which ported lib1686 (`Curl.Conformance.LibTest.UnitLibrary/Lib1686.cs`). Measured on Windows: `<verify><protocol> differs at byte 138 (line 7): expected "Authorization: Digest username="alice", realm="my-backyard", nonce="314156295", uri="/api", response="4ecc00e567c37a9d537727890c2e5b32""`, got the same fields joined by `,` with no space. The response hash already matches; only the separator differs. curl 8.22.0's `lib/vauth/digest.c` joins the fields with `, `. Find where the Libcurl path builds the Digest header differently from the tool's (the tool's Digest cases pass), and make them one.

## Acceptance criteria

- [ ] A `Libcurl` handle with `CURLOPT_HTTPAUTH` `CURLAUTH_DIGEST` sends `Authorization: Digest` fields separated by `, `, with a focused unit test in `Libcurl.UnitTests` (or the fixed library's own `.UnitTests`).
- [ ] Case 1686 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
