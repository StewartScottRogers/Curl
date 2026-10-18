---
id: BL-2692
title: Make upstream case 2118 pass: CURLOPT_SSL_VERIFYHOST without VERIFYPEER rejects a wrong-name certificate with exit 60
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2692 — Make upstream case 2118 pass: CURLOPT_SSL_VERIFYHOST without VERIFYPEER rejects a wrong-name certificate with exit 60

## Goal

Upstream case 2118 passes through the case runner and holds the ratchet, or is pinned per platform.

## Context

Found by BL-2549, which ported lib2118 (`CURLOPT_SSL_VERIFYPEER` 0 and `CURLOPT_SSL_VERIFYHOST` 1 against a server whose certificate is for `localhost.nn`). Measured on Windows: `<verify><errorcode>: expected exit code 60, got 52` - Curl accepts the wrong-name certificate when the peer is not verified, then gets no reply; curl 8.22.0 checks the host name independently of the peer (`CURLE_PEER_FAILED_VERIFICATION`). In the first pass find the project at fault (the TLS host-name check in the engine) and add it to `touches` before any edit. The case involves a certificate, so check its output on Linux and macOS before holding it on the ratchet; pin each platform's answer if they differ.

## Acceptance criteria

- [ ] Case 2118 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the difference left is filed as a task.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 4, first pass, no code edited): the project at fault is found. `Libcurl` keeps
  `CURLOPT_SSL_VERIFYHOST` apart already (`TlsPeerOptions.VerifyHost`), but
  `Curl.Engine.UnitLibrary/TlsClientOptionsMapping.Of` (and `ProxyOf`) map only `!VerifyPeer` to
  `TlsClientOptions.Insecure`, and `Curl.Networking.UnitLibrary/ServerCertificateVerification.VerifyPeer`
  returns `null` (accept) under `Insecure` before any name check. So VERIFYPEER 0 also drops the host check.
- Plan: add a `TlsClientOptions` member (default `false`, so `-k` and every caller keep today's behaviour),
  e.g. `ChecksHostNameWhenInsecure`; under `Insecure` with it set, `VerifyPeer` (and so `Judge`) refuses a
  certificate whose `WithTheNameCheckCurlRuns` errors hold `RemoteCertificateNameMismatch`, with only that
  bit passed to the build's exit 60 message (`SchannelPeerVerificationFailure` / `OpenSslPeerFailedVerification`).
  `TlsClientOptionsMapping.Of`/`ProxyOf` set it from `!VerifyPeer && VerifyHost`. Focused unit tests in
  `ServerCertificateVerificationTests` (or `SslStreamTlsProviderTests`) and `TlsClientOptionsMappingTests`.
  Then run case 2118 through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; its output is
  only the exit code, so it should not depend on the platform, but check `SslStream`'s name-mismatch flag
  is reported on Linux and macOS under an untrusted chain too before holding it.
- Not done here: the reverse (VERIFYPEER 1, VERIFYHOST 0 accepting a wrong-name trusted certificate) and
  `TlsSessionCache`'s `:NO-VRFY-HOST` key, which should then follow the new member.
- Touches widened to `Curl.Networking.UnitLibrary`, `Curl.Networking.UnitTests`, `Curl.Engine.UnitLibrary`
  and `Curl.Engine.UnitTests`. BL-2734 (in Doing) touches `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests`,
  so the task goes back to Backlog until it no longer overlaps.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 71ae8191 fd6bd9d4 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs Curl.Engine.UnitLibrary and Curl.Engine.UnitTests (TlsClientOptionsMapping), which BL-2734 in Doing touches; requeued until they no longer overlap
