---
id: BL-2690
title: Run upstream Digest libtest cases 1985 and 2026 to 2028 once Libcurl's Digest header separates fields with a comma and space
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2666]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2690 — Run upstream Digest libtest cases 1985 and 2026 to 2028 once Libcurl's Digest header separates fields with a comma and space

## Goal

Upstream cases 1985, 2026, 2027 and 2028 pass through the case runner and hold the ratchet.

## Context

Found by BL-2549, which ported lib1985 and lib2023 (libauthretry). Measured on Windows, each fails at its first Digest request with the fields joined by `,` and no space: 1985 at line 11 (`expected "Authorization: Digest username="alice", realm="host-b-realm", nonce="2", uri="/19850001", ...`), 2026, 2027 and 2028 at line 7 (`username="testuser", realm="testrealm", nonce="1", uri="/20260100"` and the like) - the same separator difference BL-2666 fixes for case 1686. Once BL-2666 is Done, rerun these cases; any that still fail differ in something else (1985 also checks that the Digest state of the first host does not leak to the second), filed as its own task.

## Acceptance criteria

- [ ] Each of 1985, 2026, 2027 and 2028 that passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing line.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
