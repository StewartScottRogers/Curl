---
id: BL-217
title: Answer Digest challenges in Curl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-151]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-217 — Answer Digest challenges in Curl.Authentication

## Goal

Digest authentication answers challenges with MD5, SHA-256, SHA-512-256, the `-sess` variants, `qop=auth` and `userhash`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item A2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- RFC 7616. The cnonce source is injected.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] RFC 7616 section 3.9 test vectors pass, and one measured curl 8.21.0 exchange is reproduced with its cnonce injected.
- [x] `dotnet build Curl.Authentication.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Authentication`.

## Notes

- Plan item: A2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0025; no task in Doing names it.
- Delivered: `DigestAuthenticator(Encoding credentialEncoding, Func<string> createClientNonce)`, an `IHttpAuthenticator` that answers the first Digest challenge when Digest is allowed and a credential exists; `DigestClientNonce.CreateRandom` for production. Internals: `DigestChallenge` (finds the first Digest element as http.c's `authcmp` does), `DigestChallengeParameters` + `DigestChallengeBuilder` (curl's `get_pair` and decoder, including the 255/1023/32 limits), `DigestAlgorithm`, `DigestQuoting`, and a hand-rolled `Sha512Slash256` (FIPS 180-4; not in the BCL).
- Scheme choice among several offered challenges is BL-218's; composing this with `BasicAndBearerAuthenticator` for `Curl.Console` is BL-218/BL-237's. Construct as `new DigestAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), DigestClientNonce.CreateRandom)`.
- **Decision (ADR-0025):** the mingw reference build does Digest through Windows SSPI (WDigest), which refuses SHA-256, SHA-512-256, userhash and md5-sess+auth-int with exit 94 and formats MD5 differently. Curl follows curl's own `lib/vauth/digest.c` (the OpenSSL build's code) on every platform, since that is the only implementation that meets this task and has source to follow.
- Stateless (ADR-0014): always `nc=00000001`; `stale` is ignored; curl's "second nonce without stale means bad credentials" check is left to the retry logic.
- Measurements (2026-09-26). Server: a loopback listener answering every request `HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: <challenge>\r\nContent-Length: 0\r\n\r\n`, 2 connections; URL `http://<host>:<port>/dir/index.html?x=1`; command `curl -s -m 5 --digest -u <user:password> <url>`.
  - mingw curl 8.21.0 (Windows, SSPI) via `Record-CurlExchange.ps1`: MD5 `qop="auth,auth-int"` -> `Authorization: Digest username="Mufasa",realm="testrealm@host.com",nonce="dcd98b7102dd2f0e8b11d0f600bfb0c093",uri="/dir/index.html?x=1",cnonce="355626b99094bc8eb8b6541e63b18fb1",nc=00000001,response="3faabfc3187546be5e073c666e073e73",qop="auth",opaque="5ccc069c403ebaf9f0171e9517f40e41"`; SHA-256, SHA-512-256-sess+userhash, md5-sess+auth-int, SHA-1, unquoted params -> exit 94, no second request; MD5-sess without qop -> answered with `algorithm=MD5-sess`, no cnonce.
  - curl 8.21.0 OpenSSL (`docker run curlimages/curl:8.21.0`, server in `python:3-alpine` on a docker network). Each value below is pinned in `DigestAuthenticatorTests` with its cnonce:
    - MD5 (`Mufasa:Circle Of Life`, cnonce `AqQxeIb4+11xINQp`) -> `response="1c3c228af43ad1467439331b0d52e7a2"`.
    - SHA-256 (`Mufasa:Circle of Life`, `KJ/mlVGS7TUHDJjH`) -> `d39ad8a3...8e36`.
    - SHA-512-256 userhash (user as Latin-1 bytes `J\xE4s\xF8n Doe`, `SRYUJbi5FSpCCXuX`) -> `username="e72804be..."`, `response="35050cff..."`.
    - SHA-512-256-sess (`Jason:pw`, `nnmPKDzp1C4nRhKe`) -> `8cf71760...`.
    - md5-sess auth-int (`S9m65TsQDYXSNo9z`) -> `d93b6bff...`, `algorithm=md5-sess` echoed as received.
    - `realm="a\"b", opaque="o\p"`, user `u"x` -> `username="u\"x", realm="a\"b"`, `opaque="op"`, no qop.
    - `realm=r r ,nonce=abc,qop=auth ,stale=TRUE` -> `realm="r r "`, no qop.
    - Two Digest headers -> the first answered.
    - `Basic realm="x", Digest ...` in one header -> the Digest answered.
    - Non-ASCII/control bytes -> `username="J%C3%A9", realm="r%E9%09x", nonce="n%01"`.
    - `userhash=TRUE, algorithm=sha-256-SESS`, no realm -> `realm=""`.
    - SHA-1, MD5-sess without qop, and no nonce -> no answer.
- Source followed: curl-8_21_0 `lib/vauth/digest.c`, `lib/http_digest.c`, `lib/http.c` (`authcmp`, `auth_digest`), fetched from GitHub. The Ubuntu curl 8.18.0 (WSL) gave the same format.
- RFC 7616 section 3.9: the printed MD5 response (`...eebdec3`), and the 3.9.2 username hash and response, are wrong in the RFC; tests pin the errata values, confirmed independently with Python `hashlib` (MD5 `8ca523f5e9506fed4657c9700eebdbec`; 3.9.2 username `793263ca...9b0b`, which curl 8.18.0 also sent; response `3798d413...68a5`). The SHA-256 example value matches the RFC as printed.
- Quality: 119 tests in `Curl.Authentication.UnitTests`; `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` gives 100% line, 100% branch, 59 members, 0 failing, worst CRAP 10. One full-solution run hit a transient failure in `Curl.Networking.UnitTests` (TLS loopback under load, not touched here); it passed on rerun and in the next full run.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. DigestAuthenticator answers MD5, SHA-256, SHA-512-256, -sess, qop auth/auth-int and userhash challenges as curl 8.21.0's own Digest code does (ADR-0025)
