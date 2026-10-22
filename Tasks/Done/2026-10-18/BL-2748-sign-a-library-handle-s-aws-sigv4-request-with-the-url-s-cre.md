---
id: BL-2748
title: Sign a library handle's AWS SigV4 request with the URL's credentials (upstream case 1978)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/EasyAuthenticationOptionsMappingTests.cs]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2748 — Sign a library handle's AWS SigV4 request with the URL's credentials (upstream case 1978)

## Goal

A `Libcurl` or `Curl.Api` handle with `CURLOPT_AWS_SIGV4` and credentials only in its URL (`http://xxx:yyy@host/`) signs the request with them, as libcurl 8.22.0 does, and upstream case 1978 passes.

## Context

Found by BL-2687. Upstream lib1978 sets no `CURLOPT_USERPWD`: the harness's URL is `http://xxx:yyy@127.0.0.1:9000/1978/testapi/test`, and libcurl's `override_login` / `parseurlandfillconn` take `xxx:yyy` from it, so the expected request carries `Authorization: AWS4-HMAC-SHA256 Credential=xxx/19700101/us-east-1/s3/aws4_request, ...`. Curl sends the PUT with no `Authorization`, `X-Amz-Date` or `x-amz-content-sha256` at all (`CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `<verify><protocol> differs at byte 55 (line 3)`).

Cause: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` `TryLookUpNetrcCredentials` returns early under `NetrcUsage.Ignored`, so `TransferCredentialLookup.TryLookUp` - which would give the URL's decoded user and password (`CredentialsWrittenInUrl`) - never runs for a library handle without `CURLOPT_NETRC`. The context's `Credentials` then stays `null`, and `AwsSigV4HttpAuthenticator.Sign` sends nothing when `request.Credential` is `null`. Run the lookup for every handle with no `ToolInputs` (it already lets `CURLOPT_USERPWD`/`CURLOPT_USERNAME` win), and check other schemes that read URL credentials elsewhere do not now get them twice.

Measured in BL-2687: adding `CURLOPT_USERPWD "xxx"` to the port makes the request signed with `Credential=xxx`, but the signature differs (empty secret instead of `yyy`), which confirms the URL password is what must be used; the port is right as it is.

## Acceptance criteria

- [x] A test in `Curl.Engine.UnitTests` performs a handle with `AwsSigV4` set and credentials only in the URL and pins that the request carries the SigV4 `Authorization` with the URL's user name.
- [x] `CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 1978 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or its new first differing line is filed as a task.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer.TryLookUpNetrcCredentials and TransferCredentialLookup, so it can run beside other Engine tasks.
- 2026-10-18: The URL-credentials lookup the Context describes had already landed with BL-2684 (`TryLookUpUrlAndNetrcCredentials` runs for every handle with no `ToolInputs`), so the first request was already signed with `Credential=xxx` and the right signature. 1978 then failed at its second request: after `CURLOPT_AWS_SIGV4` NULL, Curl sent `Authorization: Basic` with the URL credentials, because `HttpRequestOptionsMapping.AuthSchemesOf` fell back to Basic when only the `CURLAUTH_AWS_SIGV4` bit was left. libcurl keeps that bit picked (setopt only sets httpauth when the string is non-NULL) and sends no `Authorization`, as the case expects. Fix: a lone AWS SigV4 bit gives `HttpAuthSchemes.None`, not Basic. Small, follows the case byte for byte, so no ADR.
- 2026-10-18: touches widened to HttpRequestOptionsMapping.cs, its test EasyAuthenticationOptionsMappingTests.cs and Curl.Engine.UnitLibrary/CLAUDE.md (which stated the old Basic fallback); no other task in Doing on origin/work/dark-factory names them.
- 2026-10-18: Verified: case 1978 passes through NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust and is on the ratchet; dotnet build clean; fast tests green (Curl.Engine.UnitTests 2060 passed); Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 8567759c a272e763 91a39c1d.
- 2026-10-18: Doing -> Done. A library handle signs AWS SigV4 with the URL's credentials and sends no Authorization once CURLOPT_AWS_SIGV4 is NULL; upstream case 1978 passes
