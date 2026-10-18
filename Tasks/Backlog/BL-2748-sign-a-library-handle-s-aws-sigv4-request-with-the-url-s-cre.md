---
id: BL-2748
title: Sign a library handle's AWS SigV4 request with the URL's credentials (upstream case 1978)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2748 — Sign a library handle's AWS SigV4 request with the URL's credentials (upstream case 1978)

## Goal

A `Libcurl` or `Curl.Api` handle with `CURLOPT_AWS_SIGV4` and credentials only in its URL (`http://xxx:yyy@host/`) signs the request with them, as libcurl 8.22.0 does, and upstream case 1978 passes.

## Context

Found by BL-2687. Upstream lib1978 sets no `CURLOPT_USERPWD`: the harness's URL is `http://xxx:yyy@127.0.0.1:9000/1978/testapi/test`, and libcurl's `override_login` / `parseurlandfillconn` take `xxx:yyy` from it, so the expected request carries `Authorization: AWS4-HMAC-SHA256 Credential=xxx/19700101/us-east-1/s3/aws4_request, ...`. Curl sends the PUT with no `Authorization`, `X-Amz-Date` or `x-amz-content-sha256` at all (`CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: `<verify><protocol> differs at byte 55 (line 3)`).

Cause: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` `TryLookUpNetrcCredentials` returns early under `NetrcUsage.Ignored`, so `TransferCredentialLookup.TryLookUp` - which would give the URL's decoded user and password (`CredentialsWrittenInUrl`) - never runs for a library handle without `CURLOPT_NETRC`. The context's `Credentials` then stays `null`, and `AwsSigV4HttpAuthenticator.Sign` sends nothing when `request.Credential` is `null`. Run the lookup for every handle with no `ToolInputs` (it already lets `CURLOPT_USERPWD`/`CURLOPT_USERNAME` win), and check other schemes that read URL credentials elsewhere do not now get them twice.

Measured in BL-2687: adding `CURLOPT_USERPWD "xxx"` to the port makes the request signed with `Credential=xxx`, but the signature differs (empty secret instead of `yyy`), which confirms the URL password is what must be used; the port is right as it is.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests` performs a handle with `AwsSigV4` set and credentials only in the URL and pins that the request carries the SigV4 `Authorization` with the URL's user name.
- [ ] `CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 1978 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or its new first differing line is filed as a task.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer.TryLookUpNetrcCredentials and TransferCredentialLookup, so it can run beside other Engine tasks.

## Log

- 2026-10-18: Created.
