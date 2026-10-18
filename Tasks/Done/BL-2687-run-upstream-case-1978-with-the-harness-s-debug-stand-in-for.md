---
id: BL-2687
title: Run upstream case 1978 with the harness's Debug stand-in for AWS SigV4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2675]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2687 — Run upstream case 1978 with the harness's Debug stand-in for AWS SigV4

## Goal

Upstream case 1978 runs (not skipped) through the case runner and, when it passes on Windows, holds the ratchet.

## Context

Found by BL-2549, which ported lib1978 (`Curl.Conformance.LibTest.UnitLibrary/Lib1978.cs`: an empty upload signed `aws:amz:us-east-1:s3` with nineteen duplicate, differently cased and valueless headers the signature must merge, then the same upload unsigned). Measured on Windows: `test1978 is skipped: Curl lacks the feature Debug`. The case needs the same Debug stand-in (fixed SigV4 signing time) BL-2675 adds for cases 1933 to 1938; once that lands, run 1978 and, if it fails, find the project at fault (likely the SigV4 header merging) and add it to `touches` before any edit.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` is not skipped.
- [x] Case 1978 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` when it passes, or its differing line is filed as a task.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- BL-2675's Debug stand-in already covers this case (`PlatformFor` reports Debug to any case naming the `aws` feature), so no harness change was needed: `CURL_UPSTREAM_CASE=1978` now runs and fails rather than skipping.
- First difference: `<verify><protocol> differs at byte 55 (line 3)`: the first PUT goes out with no `Authorization`, `X-Amz-Date` or `x-amz-content-sha256`. The case's URL carries the credentials (`http://xxx:yyy@127.0.0.1:9000/...`); `ManagedEasyTransfer.TryLookUpNetrcCredentials` skips `TransferCredentialLookup` under `NetrcUsage.Ignored`, so a library handle never takes URL credentials and the SigV4 authenticator has none to sign with. A trial `CURLOPT_USERPWD "xxx"` in the port (reverted) signed it but with the wrong secret, confirming the URL password is needed and the port is right.
- The fix is in `Curl.Engine.UnitLibrary`, which BL-2734 (Doing) touches, so it is filed as BL-2744 rather than done here (criterion 2's "filed as a task"). 1978 stays off the ratchet until BL-2744.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 8b0c7749 8e85384c 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 1978 now runs (Debug stand-in); its failure, URL credentials not signing a library handle's SigV4 request, is filed as BL-2744
