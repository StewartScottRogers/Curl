---
id: BL-2701
title: Make upstream case 2506 send the netrc Authorization on a proxied request that follows a redirect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2696]
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2701 — Make upstream case 2506 send the netrc Authorization on a proxied request that follows a redirect

## Goal

Upstream case 2506 (lib2506) passes through the runner and is on the ratchet.

## Context

Found by BL-2551, which ported lib2506 (`Curl.Conformance.LibTest.UnitLibrary/Lib2506.cs`): `CURLOPT_PROXY` is the case's URL, `CURLOPT_URL` is `http://site.example/`, `CURLOPT_NETRC` is `CURL_NETRC_OPTIONAL` with `CURLOPT_NETRC_FILE` holding `machine site.example login batman password robin`, `CURLOPT_FOLLOWLOCATION` and `CURLOPT_UNRESTRICTED_AUTH` are on. Real curl sends `Authorization: Basic YmF0bWFuOnJvYmlu` on the first proxied `GET http://site.example/` and none on the redirect to `http://numbertwo.example/25060002`. Curl sends none on the first: `<verify><protocol>` differs at byte 56 (line 3): expected `Authorization: Basic YmF0bWFuOnJvYmlu\r\n`, got `Accept: */*\r\n`. BL-2696 (case 2309, the same `.netrc` lookup missing for a proxied host) likely shares the cause, so this task waits on it; after it, rerun with `CURL_UPSTREAM_CASE=2506` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test, and fix what remains (the credentials must not follow the redirect to another host, though `CURLOPT_UNRESTRICTED_AUTH` is on, because they came from `.netrc`).

## Acceptance criteria

- [x] Case 2506 passes through the runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Libcurl.UnitTests` (or the fixed library's own tests) covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The cause was in `Curl.Engine.UnitLibrary`, not `Libcurl`: BL-2696 looked the `.netrc` file up only for the first URL, and the handle's own `RedirectFollower` carried those credentials to every hop under `CURLOPT_UNRESTRICTED_AUTH`. `ManagedEasyTransfer` now gives its follower `TransferCredentialLookup.ForRedirectHops`, the per-hop lookup the curl tool's runner already used (BL-790), so `numbertwo.example`, with no entry, gets no `Authorization`. Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches` for it; no task in Doing on `origin/work/dark-factory` named either.
- Focused test: `ManagedEasyTransferTests.PerformAsync_NetrcOptionalThroughAProxyRedirectedToAHostWithNoEntry_SendsTheEntryOnlyToTheFirstHost`. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 7d5d7809 a29b01ae 3d836d5e.
- 2026-10-19: Doing -> Done. Case 2506 passes and is on the ratchet; netrc credentials are looked up again for each redirect hop on a library handle.
