---
id: BL-2696
title: Make upstream case 2309 send the .netrc Authorization through a proxy on a duplicated handle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2696 — Make upstream case 2309 send the .netrc Authorization through a proxy on a duplicated handle

## Goal

Upstream case 2309 (lib2309) passes through the runner and is on the ratchet.

## Context

Found by BL-2550, which ported lib2309: a handle with `CURLOPT_PROXY` (`libtest_arg3`), `CURLOPT_NETRC` `CURL_NETRC_REQUIRED` and `CURLOPT_NETRC_FILE` (`libtest_arg2`) for `http://github.com` is copied by `curl_easy_duphandle` and the copy performed. Real curl sends `Authorization: Basic ZGFuaWVsOiR5JGo5VCRXVVZqaVZ2RGJSQVdhZkRMczZjYWIxJDAxTlgub2FaS2Y1bHc4TVIyTms5WWF4djRDcWJFMElhREYuR3BHeFB1bDE=` as line 3 of the proxied `GET http://github.com/`; Curl sends none (`<verify><protocol>` differs at byte 52, line 3: expected that header, got `Accept: */*`). Case 1599 (`.netrc` without proxy or dup) passes, so the cause is either `DupHandle` not carrying the netrc options or the netrc lookup missing for a proxied host; find which first. Run it with `CURL_UPSTREAM_CASE=2309` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [ ] Upstream case 2309 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: d64931ca f0471b8d 3d836d5e.
