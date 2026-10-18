---
id: BL-2696
title: Make upstream case 2309 send the .netrc Authorization through a proxy on a duplicated handle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2696 — Make upstream case 2309 send the .netrc Authorization through a proxy on a duplicated handle

## Goal

Upstream case 2309 (lib2309) passes through the runner and is on the ratchet.

## Context

Found by BL-2550, which ported lib2309: a handle with `CURLOPT_PROXY` (`libtest_arg3`), `CURLOPT_NETRC` `CURL_NETRC_REQUIRED` and `CURLOPT_NETRC_FILE` (`libtest_arg2`) for `http://github.com` is copied by `curl_easy_duphandle` and the copy performed. Real curl sends `Authorization: Basic ZGFuaWVsOiR5JGo5VCRXVVZqaVZ2RGJSQVdhZkRMczZjYWIxJDAxTlgub2FaS2Y1bHc4TVIyTms5WWF4djRDcWJFMElhREYuR3BHeFB1bDE=` as line 3 of the proxied `GET http://github.com/`; Curl sends none (`<verify><protocol>` differs at byte 52, line 3: expected that header, got `Accept: */*`). Case 1599 (`.netrc` without proxy or dup) passes, so the cause is either `DupHandle` not carrying the netrc options or the netrc lookup missing for a proxied host; find which first. Run it with `CURL_UPSTREAM_CASE=2309` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [x] Upstream case 2309 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: neither the dup nor the proxy. A `Libcurl`/`Curl.Api` handle (no `ToolInputs`) never looked in the `.netrc` file at all: `ManagedEasyTransfer.OwnInputsOf` carried no credentials, and only the curl tool's runner called `TransferCredentialLookup`. Case 1599 passed only because its `default ... login anonymous` entry matches FTP's own anonymous default.
- Fix is in `Curl.Engine.UnitLibrary`, so `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` were added to `touches` (no task in Doing on `origin/work/dark-factory` named them). `Libcurl` needed no change.
- `ManagedEasyTransfer.TryLookUpNetrcCredentials` runs the lookup after the proxy is chosen, only when `CURLOPT_NETRC` is not `Ignored`, so URL user information keeps its existing path. The file is read through the new `EasyTransferHost.ReadFile` (default `ReadDiskFile`, the real disk), and `NETRC`/`HOME` come from the engine's injected environment reader (`ManagedEngineSharedState.ReadEnvironmentVariable`). A required file that is missing or malformed ends the perform with `ReadError` (26) and the lookup's text.
- Default taken: redirect hops are not looked up again for library handles (the curl tool's runner does that with `ForRedirectHops`). No upstream case needs it yet.
- Tests: `ManagedEasyTransferTests.Netrc.cs` (5 tests). Case 2309 is on the ratchet; its `.netrc` and output do not depend on the platform. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: d64931ca f0471b8d 3d836d5e.
- 2026-10-17: Doing -> Done. Library handles look up CURLOPT_NETRC credentials; upstream case 2309 passes and is on the ratchet
