---
id: BL-2684
title: Make upstream case 1964 pass: AWS SigV4 signing with provider xxx, credentials in the URL and an X-Xxx-Date header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2684 — Make upstream case 1964 pass: AWS SigV4 signing with provider xxx, credentials in the URL and an X-Xxx-Date header

## Goal

Upstream case 1964 passes through the case runner and holds the ratchet.

## Context

Found by BL-2548, which ported lib1964 as `Lib1964` in `Curl.Conformance.LibTest.UnitLibrary` (unit tested over a fake engine; it follows lib1964.c: `CURLOPT_AWS_SIGV4` `xxx`, no `CURLOPT_USERPWD`, the URL `http://xxx:yyy@127.0.0.1:9000/1964/testapi/test`, `CURLOPT_CONNECT_TO` and the headers `Content-Type: application/json` and `X-Xxx-Date: 19700101T000000Z`). The case needs no Debug feature: the date header fixes the signing time. Difference: `<verify><protocol> differs at byte 55 (line 3): expected "Authorization: XXX4-HMAC-SHA256 Credential=xxx/19700101/0/127/xxx4_request, SignedHeaders=content-type;host;x-xxx-date, Signature=35da102c...", got "X-Xxx-Date: 19700101T000000Z"`: the request carries no SigV4 `Authorization` before the caller's headers. Likely causes: the signer does not take the credentials from the URL's user info, or does not take the signing time from an `X-<Provider>-Date` header the caller sets. Run it with `CURL_UPSTREAM_CASE=1964` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine project at fault (the SigV4 signer) and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Upstream case 1964 passes through the case runner on Windows and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-18 (lane 3, first pass, no code edited): measured with `CURL_UPSTREAM_CASE=1964`: the request carries
  no `Authorization` and no `Server auth using AWS_SIGV4` line, so `AwsSigV4HttpAuthenticator.Sign` returned at
  `request.Credential is null`. Cause: `ManagedEasyTransfer.TryLookUpNetrcCredentials` returns early when
  `EasyAuthenticationOptions.Netrc == NetrcUsage.Ignored`, so a library handle never gets the URL's user info
  (`xxx:yyy`) as its credential; `TransferCredentialLookup.TryLookUp` already returns `CredentialsWrittenInUrl(url)`
  under `Ignored`, so the fix is to drop the `Ignored` early return (keep the `ToolInputs` one), rename the method and
  field to say they look up the URL's and the netrc file's credentials, and add a `ManagedEasyTransferTests` test
  (URL credentials sent with no `CURLOPT_USERPWD`, e.g. Basic, and a SigV4-signed request). The signer itself
  (Curl.Authentication) is not at fault as far as measured. Added `Curl.Engine.UnitLibrary` and
  `Curl.Engine.UnitTests` to `touches`; requeued because BL-2734 (in Doing) touches both.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 29fc16eb fd6bd9d4 4272a6b9 8738c7f6.
- 2026-10-18: Doing -> Backlog. Needs Curl.Engine.UnitLibrary and Curl.Engine.UnitTests, which BL-2734 (in Doing) touches; fix diagnosed under Notes
