---
id: BL-2725
title: Make upstream case 678 pass: lib678's CURLOPT_CAINFO_BLOB transfer reaches the HTTPS stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2725 — Make upstream case 678 pass: lib678's CURLOPT_CAINFO_BLOB transfer reaches the HTTPS stand-in

## Goal

Upstream case 678 passes through the runner and holds the ratchet.

## Context

Found by BL-2634, which made the runner run test678's `<precheck>` line `./libtest/libtests lib678 check`
through the ported `Lib678` (the precheck now passes quietly, so the case runs). The case then fails on
Windows with:

```
test678 fails: <verify><protocol> differs at byte 0 (line 1): expected "GET /678 HTTP/1.1\r\n", got the end
- 1: "GET /678 HTTP/1.1\r\n"
- 2: "Host: localhost:8989\r\n"
- 3: "User-Agent: CURLOPT_CAINFO_BLOB\r\n"
- 4: "Accept: */*\r\n"
- 5: "\r\n"
```

No request reaches the HTTPS stand-in (`https test-localhost.pem` on `%HTTPSPORT`), and the program's stdout
and stderr were both empty. Lib678 passes `%CERTDIR/certs/test-ca.crt` as `libtest_arg2`, reads it with
`LoadFile` and sets it as `CURLOPT_CAINFO_BLOB` with `CURLSSLOPT_REVOKE_BEST_EFFORT`. Find first whether the
file is read (an unread file ends with `OutOfMemory` before any transfer) or the TLS handshake against the
blob fails in the `Libcurl` engine (`Libcurl/Options/Tls/EasyTlsOptionSetters.cs` and the engine's TLS
trust), then add the project at fault to `touches` before any edit. Reproduce with `CURL_UPSTREAM_CASE=678`
and the `Manual` test of `Curl.Conformance.UnitTests` (see its `CLAUDE.md`). Check the case is
platform-neutral before holding it (Schannel on Windows, OpenSSL elsewhere; revocation best effort).

## Acceptance criteria

- [x] test678 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, if it still fails, its differing lines are filed with the board script.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 2, diagnosis, no code changed): reproduced with `CURL_UPSTREAM_CASE=678`. The file is
  read: the kept `stderr678` is `URL: https://localhost:8989/678` and `Test ended with result 60`
  (`CURLE_PEER_FAILED_VERIFICATION`). Cause: `CURLOPT_CAINFO_BLOB` stops at `Curl.Api.UnitLibrary`'s
  `TlsPeerOptions.CertificateAuthorityBlob` (set by `Libcurl/Options/Tls/TlsPeerOptionSetters.cs:87`) and
  nothing reads it. `Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs` (`Of`, `ProxyOf`, `DohOf`) maps
  only `CertificateAuthorityFile`, and `Curl.Networking.UnitLibrary/TlsClientOptions.cs` has no blob member,
  so `ServerCertificateVerification` (`CaCertificateFile is null` at lines 79/149/232/275, `ReadCaCertificateFile`
  at 320) verifies against the system store and rejects the test CA.
- Fix plan: add `CaCertificateBlob` (`ReadOnlyMemory<byte>?`) to `TlsClientOptions`; in
  `ServerCertificateVerification` treat a blob like a CA file (custom trust store from the PEM/DER bytes,
  winning over the file, as libcurl's blob does), set `TlsTrustEvent.HasCaCertificateBlob` so `-v` prints
  `CA Blob from configuration` (OpenSSL build; `OpenSslTrustText` already handles it); map
  `origin.CertificateAuthorityBlob` / `proxy.CertificateAuthorityBlob` in `TlsClientOptionsMapping.Of`,
  `ProxyOf` and `DohOf`. Check the hand-built provider (`HandBuiltTlsProvider`) and `QuicDialer` (line 413)
  read the same member. Focused unit tests in `Curl.Networking.UnitTests` and `Curl.Engine.UnitTests`.
- Requeued because those projects overlap live tasks: `Curl.Networking.*` with BL-2730 and `Curl.Engine.*`
  with BL-2735 (added to `touches`, rule 3).
- 2026-10-19 (lane 1): fixed as planned. `TlsClientOptions.CaCertificateBlob` (with `HasCaCertificates`) is
  trusted in place of the CA file by `ServerCertificateVerification` and QUIC's Windows-store choice;
  `DescribeTrust` sets `HasCaCertificateBlob`; `TlsClientOptionsMapping.Of` and `DohOf` carry the origin's
  blob, `ProxyOf` the proxy's (ADR-0704). test678 passes through the runner and is on the ratchet. It is
  platform-neutral: the blob is the case's PEM test CA, read by the same rules as `--cacert` on both
  builds, and the OpenSSL build never checks revocation. Unit tests: 4 in `SslStreamTlsProviderTests`,
  1 in `TlsClientOptionsMappingTests`. Measure-CodeQuality for Networking and Engine: 0 failing members.
  Narrowing the Engine `touches` (step 2a) was skipped: the permission layer refused the push.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 262ba952 c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs Curl.Networking.UnitLibrary/UnitTests (held by BL-2730) and Curl.Engine.UnitLibrary/UnitTests (held by BL-2735): CURLOPT_CAINFO_BLOB never reaches TlsClientOptions, so verification fails with 60
- 2026-10-19: Backlog -> Doing. Criteria at claim: 262ba952 c59fcab3 3d836d5e.
- 2026-10-19: Doing -> Done. CURLOPT_CAINFO_BLOB is trusted like a CA file; upstream test678 passes and holds the ratchet
