---
id: BL-2693
title: Sign AWS SigV4 with URL credentials in a libcurl handle (upstream test1933)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs, Curl.Engine.UnitTests/AwsSigV4HttpAuthenticatorTests.cs, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2693 — Sign AWS SigV4 with URL credentials in a libcurl handle (upstream test1933)

## Goal

Upstream case 1933 passes: a Libcurl handle with CURLOPT_AWS_SIGV4 and credentials only in the URL (`http://xxx:yyy@127.0.0.1:9000/...`) sends the SigV4 Authorization and X-Xxx-Date lines.

## Context

Found by BL-2675, which made the AWS SigV4 libtest cases run with the harness's Debug stand-in and an epoch signing clock. test1933 (lib1933, LibAwsSigV4Request.Lib1933, sets only CURLOPT_AWS_SIGV4 "xxx") fails: the request carries no Authorization line at all, so the engine's perform from the options does not take the URL's user name and password as the SigV4 credentials (the curl tool's -u-less URL credentials do work, see test3222). Expected line 3: `Authorization: XXX4-HMAC-SHA256 Credential=xxx/19700101/0/127/xxx4_request, SignedHeaders=content-type;host;x-xxx-date, Signature=3d8e00a02e437211a596143dcd590fcc805b731365c68f7f48951ea6eda39c4f`; got the blank line ending the head. Start at ManagedEasyTransfer's credential lookup (TransferCredentialLookup) and AwsSigV4HttpAuthenticator. Run it with `CURL_UPSTREAM_CASE=1933` and NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=1933` with NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust passes, and 1933 is on Curl.Conformance.UnitTests/PassingUpstreamCases.txt.
- [ ] A focused unit test in Curl.Engine.UnitTests pins a SigV4 request signed with URL credentials (ADR-0482).
- [ ] dotnet build is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer.TryLookUpNetrcCredentials, TransferCredentialLookup and AwsSigV4HttpAuthenticator, so it can run beside other Engine tasks.

## Log

- 2026-10-17: Created.
