---
id: BL-2627
title: Make upstream case 655 pass: call CURLOPT_RESOLVER_START_FUNCTION before resolving
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Documentation/Planning/Decisions/ADR-0611-prerequest-and-resolver-start-callbacks-in-the-engine.md]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2627 — Make upstream case 655 pass: call CURLOPT_RESOLVER_START_FUNCTION before resolving

## Goal

Upstream case 655 (lib655, a resolver start callback that refuses, then one that allows) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 655 stdout `expected "hello\n", got the end`; stderr shows `Could not resolve host: failthis` and `curl_easy_perform should have returned CURLE_ABORTED_BY_CALLBACK but instead returned error 6`: the engine never calls the resolver start callback. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Root cause: the engine did call the resolver-start callback (BL-2419's `ResolverStartDnsResolver`), but a refusal
  failed with exit 6. ADR-0611 had pinned exit 6, claiming test655 checks it; lib655.c requires
  `CURLE_ABORTED_BY_CALLBACK` (42), as curl 8.22.0's `Curl_resolv` returns. Decided by Claude under Stewart's
  delegation: keep the plain `Could not resolve host: <h>` text (resolve_host's failf for any resolve error) with code 42.
- Fix: new `DnsLookupFailure.AbortedByCallback` (Curl.Networking), which `NameResolutionFailure.Describe` maps to exit
  42 with the plain message (no c-ares reason); `ResolverStartDnsResolver` returns it on a refusal. ADR-0611 amended in
  place (BL-2627 amendment) rather than a new ADR, since only its exit code changed.
- Touches widened to Curl.Networking.UnitLibrary/UnitTests, Curl.Engine.UnitLibrary/UnitTests and ADR-0611: no other
  task was in Doing on origin/work/dark-factory.
- Focused tests: `NameResolutionFailureTests.Describe_AbortedByCallback_IsExit42WithThePlainMessage`, a
  `DnsLookupFailureTextTests` row, `ResolverStartDnsResolverTests` and
  `ManagedEasyTransferTests.PerformAsync_ResolverStartCallbackAborts_ReturnsAbortedByCallbackWithoutConnecting`.
- 655 passes through the runner on Windows and is on the ratchet. Platform-neutral: the refused name is never looked
  up, the second URL is the loopback server's IP, and only the body and exit code are compared.
- Measure-CodeQuality for Curl.Networking.UnitLibrary and Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 655 passes: a refusing resolver-start callback ends the transfer with CURLE_ABORTED_BY_CALLBACK (42)
