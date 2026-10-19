---
id: BL-2823
title: HTTP: keep Digest available after a refused auth answer as curl's authproblem does (upstream case 2030)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2823 — HTTP: keep Digest available after a refused auth answer as curl's authproblem does (upstream case 2030)

## Goal

`TransferReport.DigestStaysAvailable` is set exactly when libcurl 8.22.0 leaves `CURLAUTH_DIGEST` in `authhost.avail` at the end of a transfer, so upstream case 2030's request 400 is sent twice as curl sends it.

## Context

- Found by an interactive session on 2026-10-19 working BL-2744, with a temporary trace through `ManagedEasyTransfer` and `HttpProtocolHandler` (removed afterwards).
- Upstream case 2030 runs lib2023: transfers 100 and 200 on one easy handle, then transfers 300, 400 and 500 on a **new** handle (`Lib2023.RequestsOnOneHandleAsync`). So no Digest nonce carries from 200 into 300.
- Transfer 300 (`CURLAUTH_NTLM`, wrong password) sends NTLM type 1, gets `data1301` (NTLM type 2), sends type 3, and gets `data1302`: `401` with `WWW-Authenticate: NTLM` then `Digest realm="testrealm", nonce="5"`.
  - In curl, the bare `NTLM` header after type 3 is a refusal. `Curl_http_input_auth` sets `data->state.authproblem`.
  - The `Digest` header in the same 401 adds `CURLAUTH_DIGEST` to `avail` and stores nonce 5.
  - `Curl_http_auth_act` skips `pickoneauth` because `authproblem` is set, so `avail` keeps Digest into transfer 400.
  - In 400, the first 401 offering Digest is "Ignoring duplicate digest auth header." and the request is resent with nonce 5. The second 401 fails it.
- Curl's rule today (`DigestStaysAvailableAfter`, BL-2820) sets it only when the transfer already held a Digest nonce. In transfer 300 none was held, so Curl reports `false` and sends request 400 once (`test2030 fails: <verify><protocol> differs at byte 1174 (line 31)`).
- What to port: libcurl 8.22.0 `lib/http.c` `Curl_http_input_auth` (per scheme: Negotiate, NTLM, Digest, Basic, Bearer; where each sets `authproblem` or changes `avail`, and that header order matters) and `Curl_http_auth_act`/`pickoneauth`. Derive `DigestStaysAvailable` from that state, so that 2024, 2027 and 2030 all follow from the one model. `RepeatsKeptDigestAnswer` (BL-2745, ADR-0700) should then key on that state too (its `AuthSchemePicked == Digest` test is the 2027 special case).
- Upstream cases to keep green: 2023 to 2031 (lib2023 variants), especially 2024 and 2027, which pass today.

## Acceptance criteria

- [x] `HttpProtocolHandler` decides whether Digest stays available from a port of `Curl_http_input_auth`'s `authproblem` and `avail` handling, with unit tests in `Curl.Protocol.Http.UnitTests`: an NTLM refusal whose 401 also offers Digest (2030's transfer 300) keeps Digest available; a first Digest challenge alone does not (2024's transfer 300).
- [x] With it, `CURL_UPSTREAM_CASE=2030` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` sends request 400 twice, and 2024 and 2027 still pass.
- [x] An ADR records the ported rule, decided by Claude under Stewart's delegation.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Notes

- Ported the two `authproblem` sources the lib2023 cases reach: a non-stale Digest challenge while a nonce is held (BL-2820) and a bare `NTLM` challenge to a request sent with an NTLM answer (`Curl_input_ntlm` "handshake rejected"). `DigestStaysAvailableAfter` = final 401 offers Digest and `SetsAuthProblem`. Header order does not matter: Digest joins `avail` either way. ADR-0709.
- `RepeatsKeptDigestAnswer` now keys on `HttpRequestOptions.DigestStaysAvailable` instead of `AuthSchemePicked == Digest`; the 2024/2027 unit tests were rewritten to drive that state.
- Measured: `CURL_UPSTREAM_CASE` 2023 to 2031 all pass through the runner. 2030 is not added to the ratchet here (not in `touches`); BL-2744 holds it.
- Negotiate, Basic and Bearer `authproblem` sources are not modelled; no lib2023 case reaches them.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 61957160 fbb260a8 207ba9cf 72b2b029.
- 2026-10-19: Doing -> Done. Digest stays available after a 401 that sets curl's authproblem (NTLM refusal or held-nonce Digest), so upstream 2030 sends request 400 twice
