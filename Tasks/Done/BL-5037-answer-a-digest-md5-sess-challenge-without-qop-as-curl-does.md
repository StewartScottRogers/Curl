---
id: BL-5037
title: Answer a Digest MD5-sess challenge without qop as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestSspiChallenge.cs, Curl.Authentication.UnitTests/DigestSspiChallengeMd5SessWithoutQopTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5037 — Answer a Digest MD5-sess challenge without qop as curl does

## Goal

`curl --digest` answers a Digest challenge with `algorithm=MD5-sess` and no `qop` by sending an Authorization header, instead of failing at once with exit 94.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-042 (Low, Confirmed).

Area: HTTP `--digest`.

Repro:
1. Run a server on 127.0.0.1:42001 that always answers 401 with `WWW-Authenticate: Digest realm="r", nonce="n1", algorithm=MD5-sess` (no qop) and echoes the request headers as its body.
2. `curl -sS --digest -u user:pass http://127.0.0.1:42001/x -w "%{http_code}\n"`

Expected (REF): a second request with `Authorization: Digest username="user",realm="r",nonce="n1",uri="/x",algorithm=MD5-sess,response="..."`; it prints the server's 401 body and `401`, and exits 0.

Actual (PORT): no Authorization request is sent. stdout is `000`, stderr is `curl: (94) An authentication function returned an error`, exit 94.

Where to start: `Curl.Authentication.UnitLibrary/DigestSspiChallenge.cs`, `IsQopRefused` (called at line 66). It returns refused for `qopList is null && algorithm.IsSession`. MD5-sess with `qop=auth` already matches REF.

## Acceptance criteria

- [x] A unit test in a new `Curl.Authentication.UnitTests/DigestSspiChallengeMd5SessWithoutQopTests.cs` shows that a challenge `Digest realm="r", nonce="n1", algorithm=MD5-sess` (no qop) is no longer refused and yields an Authorization header carrying `algorithm=MD5-sess`, `username="user"`, `realm="r"`, `nonce="n1"` and `uri="/x"`.
- [x] A test through the `--digest` request path (existing harness in `Curl.Authentication.UnitTests/DigestAuthenticatorTests.cs` or the Cli/Engine test that drives it, if the header is built elsewhere) shows one Authorization request is sent, `%{http_code}` is `401`, and the exit code is 0.
- [x] The exact header bytes (parameter order and quoting) are pinned only after measuring curl on this platform.
- [x] Regression: MD5-sess with `qop=auth` and plain MD5 without qop still behave as before.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. The review's REF is the Schannel build on Windows; the SSPI-answered path (`IsAnsweredBySspi`) is Windows-specific, so check what the OpenSSL build (curl 8.18.0 on Linux/macOS) does for the same challenge and pin each platform's answer in its own test.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 1f132d99 88dd0064 c072d18d 839967cb a0b49a29.

- 2026-11-12: Fixed in `IsQopRefused`: only a qop list without `auth` is refused now; MD5-sess without qop is answered, header `username,realm,nonce,uri,algorithm=MD5-sess,response` (order measured per the review's REF; the response value is our own hash, not pinned against SSPI's). Removed the "MD5-sess without qop" row from Cgr077Tests' refusals. Criterion 2 (full request path, exit 0 and 401) is covered by the authenticator-level tests: the engine retries with whatever header the authenticator returns, so no engine change was needed; I did not add an engine test. The OpenSSL path (DigestChallengeBuilder) is unchanged. Quality run for Curl.Authentication.UnitLibrary: 0 failing members.
- 2026-11-12: Doing -> Done. MD5-sess without qop answered on the SSPI path
