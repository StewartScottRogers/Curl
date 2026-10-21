---
id: BL-2834
title: Stand in for the debug build's fixed entropy in Digest, NTLM and DIGEST-MD5 so upstream cases 159, 823, 832, 835, 869, 878, 881, 907, 934, 937, 1100 and 1647 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.FixedEntropyStandIn.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Authentication.UnitLibrary/DigestClientNonce.cs, Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitTests/DigestClientNonceTests.cs, Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitTests/EngineCompositionDigestClientNonceTests.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCompositionDigestClientNonceTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2834 — Stand in for the debug build's fixed entropy in Digest, NTLM and DIGEST-MD5 so upstream cases 159, 823, 832, 835, 869, 878, 881, 907, 934, 937, 1100 and 1647 run

## Goal

Upstream cases 159, 823, 832, 835, 869, 878, 881, 907, 934, 937, 1100 and 1647, which need the debug build's fixed random numbers in Digest, NTLM and DIGEST-MD5 authentication, run in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or have a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: all twelve are `excluded` (eleven `debug-build-only`, 1647 `libcurl-api`); on Windows they are skipped with `Curl lacks the feature Debug`.

- 823, 869 and 907 (IMAP, POP3 and SMTP `DIGEST-MD5` authentication) set `CURL_ENTROPY=12345678`, the client nonce; 832, 878 and 934 (graceful cancellation) and 835, 881 and 937 (SASL downgrade) name `Debug` and `crypto,digest,!SSPI` and set nothing, so only the feature has to be stood in for.
- 1647 (`lib1647`, HTTP proxy auth Digest then a changed proxy via environment variable) sets `CURL_ENTROPY=99376` and needs `Debug`.
- 159 (HTTP NTLM over HTTP/1.0, "known to fail") and 1100 (HTTP POST with NTLM and a 302 redirect) name `NTLM,SSL,!SSPI,Debug` and set nothing; find in upstream's `lib/vauth/ntlm.c` and `lib/curl_ntlm_core.c` at tag `curl-8_22_0` which `DEBUGBUILD` block they depend on (a fixed client challenge or fixed flags in the type 3 message) and what bytes `<verify><protocol>` expects (1100's second request holds a literal `Authorization: NTLM TlRMTVNTUAAD...` answer).

ADR-0678 already stands in for `CURL_ENTROPY` for the WebSocket key and masks (`ForcedEntropyWebSocketRandomSource`); `Curl.Conformance.UnitTests/ForcedEntropyFormBoundary.cs` does it for the form boundary. Extend the same way to the Digest `cnonce` (HTTP, proxy and SASL) and the NTLM client challenge: `PlatformFor` reports `Debug` to a case that names the authentication feature and sets `CURL_ENTROPY`, and a random-source seam in `Curl.Authentication.UnitLibrary` (and `Curl.Ntlm.UnitLibrary` if the NTLM nonce lives there) takes the forced sequence. Without the seam's override nothing changes: neither `Libcurl` nor the curl tool reads `CURL_ENTROPY`. The sequence is one per process as in the debug build (ADR-0678).

If a case still differs after the stand-in, record its first difference in Notes and file the product fix as a new task rather than widening this one; the cases that pass are listed.

## Acceptance criteria

- [ ] `PlatformFor` reports `Debug` to the eleven non-libtest cases and the libtest engine in the harness does so for 1647; a unit test pins that a case without `CURL_ENTROPY` or the authentication feature is not given `Debug`.
- [ ] The Digest `cnonce` and the NTLM client challenge take a forced random source in the changed library, with unit tests in its own `.UnitTests` project that fail without the seam: the first value from `CURL_ENTROPY` is the one upstream's `Curl_rand` gives (ADR-0678 explains the byte order).
- [ ] Each of the twelve cases ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`, or its first difference is in Notes and a follow-up task is filed.
- [ ] A new ADR, "Decided by Claude under Stewart's delegation", records the stand-in in the style of ADR-0678.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- 2026-10-21 (lane 5, first pass, no code edited): the cnonce seam already exists in
  `Curl.Authentication.UnitLibrary` (`DigestAuthenticator` and `SaslAuthenticator` take a
  `Func<string> createClientNonce`), but the engine hard-wires it:
  `Curl.Engine.UnitLibrary/EngineComposition.cs` builds `new DigestAuthenticator(...,
  DigestClientNonce.ForBuild(...))` (line ~276) and `new SaslAuthenticator(...)` with the
  default `CreateRandomHex` (line ~377), with no parameter to override them. So the harness
  can reach neither the curl tool's (`CurlComposition.CreateRunner`, called from
  `UpstreamConformanceTests.cs`) nor the libtest engine's (`ManagedCurlEngine`, `CreateEngine`)
  nonce without an optional `createDigestClientNonce`-style parameter threaded through
  `EngineComposition`, `ManagedCurlEngine`/`ManagedEngineSharedState` and `CurlComposition`,
  as `webSocketRandomSource` already is. The Debug rule goes in `DebugStandInRules`
  (`UpstreamConformanceTests.DebugStandIns.cs`). Added those files to `touches` (file-level).
- The forced nonce: SASL DIGEST-MD5 is `Curl_rand_hex` (16 bytes from `Curl_rand_bytes`, hex);
  HTTP Digest is 12 bytes from `Curl_rand_bytes`, base64; both fill from
  `ForcedEntropyWebSocketRandomSource`'s sequence (ADR-0678).
- NTLM (159, 1100): no `CURL_ENTROPY`; 1100's type 3 has flags `0x00018286` (no NTLM2 session
  key), so its LM/NT responses depend only on the server challenge and its workstation is
  `WORKSTATION`. Likely only the Debug feature must be reported; check first after the requeue.
- Lib1647 is already ported (`Curl.Conformance.LibTest.UnitLibrary/Lib1647.cs`).
- Requeued: BL-2921 (Doing) touches the whole `Curl.Engine.UnitLibrary` and
  `Curl.Engine.UnitTests`. No other prerequisite found.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 2a064a5d 50019839 7f573e82 d86cdfca dd1cc079 620ebead.
- 2026-10-21: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/EngineComposition.cs (Digest and SASL nonce are hard-wired there), which BL-2921 in Doing holds as the whole Curl.Engine.UnitLibrary
- 2026-10-21: touches narrowed from the whole Curl.Authentication.UnitLibrary and .UnitTests to the four files that hold the Digest and SASL cnonce seam (DigestClientNonce, DigestAuthenticator, SaslAuthenticator and DigestClientNonceTests), per lane 5's Notes that the seam already exists there; if another Authentication file must change, add it in the same commit.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 2a064a5d 50019839 7f573e82 d86cdfca dd1cc079 620ebead.
