---
id: BL-2694
title: Send CURLOPT_HEADER lines in libcurl's order for an AWS SigV4 PUT (upstream test1978)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs, Curl.Engine.UnitTests/AwsSigV4HttpAuthenticatorTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2694 — Send CURLOPT_HEADER lines in libcurl's order for an AWS SigV4 PUT (upstream test1978)

## Goal

Upstream case 1978 passes: a Libcurl AWS SigV4 PUT with CURLOPT_HTTPHEADER lines sends Authorization and the date line right after Host, before the custom headers, as libcurl 8.22.0 does.

## Context

Found by BL-2675 (AWS SigV4 libtest cases with the harness's Debug stand-in and an epoch signing clock). test1978 (lib1978) differs at line 3: expected `Authorization: AWS4-HMAC-SHA256 Credential=xxx/19700101/us-east-1/s3/aws4_request, SignedHeaders=curr-header-no-colon;duplicate-header;...`, got `x-amz-meta-test: test2` - the custom headers come before the signature lines. The curl tool's -H case test1976 passes, so look at how the engine's perform orders EasyHttpOptions headers against the authenticator's lines, and at the header set lib1978 gives (empty-valued and colon-less headers). Run it with `CURL_UPSTREAM_CASE=1978`.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=1978` with NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust passes, and 1978 is on Curl.Conformance.UnitTests/PassingUpstreamCases.txt.
- [x] A focused unit test in Curl.Engine.UnitTests pins the header order (ADR-0482).
- [x] dotnet build is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer request building, HttpRequestOptionsMapping and AwsSigV4HttpAuthenticator, so it can run beside other Engine tasks.

- 2026-10-19: BL-2748 (c29c9f05e) had already made case 1978 pass and put it on the ratchet: a lone CURLAUTH_AWS_SIGV4 bit had fallen back to Basic, so the request carried Basic auth and no SigV4 lines; the order itself was right. Measured: CURL_UPSTREAM_CASE=1978 passes. This run added only the focused test PerformAsync_AwsSigV4WithCustomHeaders_SendsTheSignatureLinesAfterHostAndBeforeTheCustomHeaders, in Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs beside BL-2748's SigV4 perform tests (added to touches; no task in Doing names it). No production code changed, so no quality run.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 3d4cb26d 30387b51 5ea3895a.
- 2026-10-18: Doing -> Done. Case 1978 passes (BL-2748's fix) and a focused engine test pins the SigV4 header order
