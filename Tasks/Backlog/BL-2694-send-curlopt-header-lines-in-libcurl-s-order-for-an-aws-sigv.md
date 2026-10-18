---
id: BL-2694
title: Send CURLOPT_HEADER lines in libcurl's order for an AWS SigV4 PUT (upstream test1978)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs, Curl.Engine.UnitTests/AwsSigV4HttpAuthenticatorTests.cs, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2694 — Send CURLOPT_HEADER lines in libcurl's order for an AWS SigV4 PUT (upstream test1978)

## Goal

Upstream case 1978 passes: a Libcurl AWS SigV4 PUT with CURLOPT_HTTPHEADER lines sends Authorization and the date line right after Host, before the custom headers, as libcurl 8.22.0 does.

## Context

Found by BL-2675 (AWS SigV4 libtest cases with the harness's Debug stand-in and an epoch signing clock). test1978 (lib1978) differs at line 3: expected `Authorization: AWS4-HMAC-SHA256 Credential=xxx/19700101/us-east-1/s3/aws4_request, SignedHeaders=curr-header-no-colon;duplicate-header;...`, got `x-amz-meta-test: test2` - the custom headers come before the signature lines. The curl tool's -H case test1976 passes, so look at how the engine's perform orders EasyHttpOptions headers against the authenticator's lines, and at the header set lib1978 gives (empty-valued and colon-less headers). Run it with `CURL_UPSTREAM_CASE=1978`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=1978` with NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust passes, and 1978 is on Curl.Conformance.UnitTests/PassingUpstreamCases.txt.
- [ ] A focused unit test in Curl.Engine.UnitTests pins the header order (ADR-0482).
- [ ] dotnet build is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer request building, HttpRequestOptionsMapping and AwsSigV4HttpAuthenticator, so it can run beside other Engine tasks.

## Log

- 2026-10-17: Created.
