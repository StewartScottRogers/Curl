---
id: BL-2689
title: Run upstream NTLM libtest cases 2025 and 2029 to 2032 once the type-1 message carries curl's flags
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2635]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2689 — Run upstream NTLM libtest cases 2025 and 2029 to 2032 once the type-1 message carries curl's flags

## Goal

Upstream cases 2025, 2029, 2030, 2031 and 2032 pass through the case runner and hold the ratchet.

## Context

Found by BL-2549, which ported lib2023 (libauthretry, cases 2023 to 2031) and lib2032 (libntlmconnect). Measured on Windows, each fails at its first NTLM request: `expected "Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", got "Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=="` (2025 at line 8, 2029 to 2031 at line 3, 2032 at line 13) - the same type-1 message difference BL-2635 fixes for case 694. Once BL-2635 is Done, rerun these cases; any that still fail differ in something else, filed as its own task.

## Acceptance criteria

- [x] Each of 2025, 2029, 2030, 2031 and 2032 that passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task with its differing line.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: Ran each case alone with `CURL_UPSTREAM_CASE` on Windows after BL-2635: 2025, 2029, 2031 and 2032 pass and are on the ratchet. 2030 still fails at line 13 (the Digest request goes out without its Authorization header: expected `Authorization: Digest username="testuser", ...`, got `Accept: */*`); filed as BL-2744.
- Platform check: these cases compare HTTP request bytes and NTLM/Digest hashes only - no paths, OS error text or certificates - and their sibling 2023 already holds the ratchet on all three CI platforms.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 9a91aaa2 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream NTLM cases 2025, 2029, 2031 and 2032 hold the ratchet; 2030 filed as BL-2744
