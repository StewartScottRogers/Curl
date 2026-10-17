---
id: BL-2663
title: Send preemptive Basic Proxy-Authorization from CURLOPT_PROXYUSERPWD through Libcurl (case 1648)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2663 — Send preemptive Basic Proxy-Authorization from CURLOPT_PROXYUSERPWD through Libcurl (case 1648)

## Goal

`CURLOPT_PROXYUSERPWD` without `CURLOPT_PROXYAUTH` sends `Proxy-Authorization: Basic` on the first request through an HTTP proxy, and none once it is cleared, so upstream case 1648 (lib1648) passes.

## Context

Found by BL-2543, which ported lib1648. The runner reports `differs at byte 53 (line 3): expected "Proxy-Authorization: Basic dmljdGltOnNlY3JldA==\r\n", got "Accept: */*\r\n"`: the proxy credentials set through Libcurl are not sent. The proxies are `firstproxy:<port>` and `secondproxy:<port>`, resolved by `CURLOPT_RESOLVE`. Compare BL-2660 (Digest, case 1588), which may share the cause.

## Acceptance criteria

- [ ] Upstream case 1648 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
